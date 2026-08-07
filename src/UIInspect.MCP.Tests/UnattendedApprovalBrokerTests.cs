// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Windows.Security;

namespace UIInspect.MCP.Tests;

/// <summary>Exercises the real current-user named-pipe broker transport without bypassing production approval UI.</summary>
public sealed class UnattendedApprovalBrokerTests
{
    /// <summary>Deterministic broker start time.</summary>
    private const string InitialUtcText = "2026-07-27T12:00:00Z";

    /// <summary>Shortest supported approval window.</summary>
    private const int ApprovalHours = 1;

    /// <summary>Whole-hour duration deliberately absent from the fixed choices.</summary>
    private const int InvalidApprovalHours = 3;

    /// <summary>Concurrent clients used to exceed the number of active broker listeners.</summary>
    private const int ConcurrentClients = 16;

    /// <summary>Status attempts allowed while the broker creates its listeners.</summary>
    private const int StatusPollAttempts = 5;

    /// <summary>Delay between broker status attempts.</summary>
    private const int StatusPollMilliseconds = 50;

    /// <summary>Maximum time allowed for broker shutdown after revocation.</summary>
    private const int BrokerShutdownSeconds = 5;

    /// <summary>Capability bit deliberately outside the approval ceiling.</summary>
    private const UiCapability UnsupportedCapability = (UiCapability)8;

    /// <summary>Broker exit code when a scope is already owned.</summary>
    private const int DuplicateBrokerExitCode = 2;

    /// <summary>One broker accepts concurrent exact-target checks and stops immediately when revoked.</summary>
    /// <returns>A task that verifies the real local transport.</returns>
    [Test]
    public async Task Broker_serves_concurrent_clients_and_revokes()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(InitialUtcText));
        using var currentProcess = Process.GetCurrentProcess();
        var target = new ProcessIdentity(
            currentProcess.Id,
            time.UtcNow.AddHours(-ApprovalHours),
            currentProcess.ProcessName,
            Environment.ProcessPath ?? string.Empty,
            currentProcess.SessionId);
        var processes = new FakeProcessIdentityProvider { Current = target, };
        var audit = new FakeAuditSink();
        var scope = CreateUniqueScope(currentProcess.SessionId);
        using var broker = new WindowsUnattendedApprovalBroker(
            time,
            processes,
            scope,
            audit,
            static (_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(true);
            });
        var authorizer = new WindowsUnattendedApprovalAuthorizer(scope);
        using var brokerCancellation = new CancellationTokenSource();
        var brokerTask = Task.Run(() => broker.RunAsync(ApprovalHours, brokerCancellation.Token));

        try
        {
            var lease = await WaitForLeaseAsync(authorizer);
            await Assert.That(lease).IsNotNull();
            await Assert.That(lease!.Capabilities).IsEqualTo(
                UiCapability.Inspect | UiCapability.Interact | UiCapability.Keyboard);
            await ValidateConcurrentlyAsync(authorizer, lease, target);

            await Assert.That(
                await authorizer.GetActiveLeaseAsync(UnsupportedCapability, CancellationToken.None)).IsNull();
            await Assert.That(
                await authorizer.ValidateAsync(Guid.NewGuid(), target, UiCapability.Inspect, CancellationToken.None)).IsFalse();

            processes.Current = target with { StartedAtUtc = target.StartedAtUtc.AddSeconds(1), };
            await Assert.That(
                await authorizer.ValidateAsync(lease.LeaseId, target, UiCapability.Inspect, CancellationToken.None)).IsFalse();
            processes.Current = target;

            await Assert.That(await authorizer.RevokeAsync(CancellationToken.None)).IsTrue();
            await Assert.That(await brokerTask.WaitAsync(TimeSpan.FromSeconds(BrokerShutdownSeconds))).IsEqualTo(0);
            await Assert.That(await authorizer.GetStatusAsync(CancellationToken.None)).IsNull();
            await Assert.That(ContainsAuditEvent(audit.Snapshot(), "unattended_approval_granted")).IsTrue();
            await Assert.That(ContainsAuditEvent(audit.Snapshot(), "unattended_approval_revoked")).IsTrue();
        }
        finally
        {
            _ = await authorizer.RevokeAsync(CancellationToken.None);
            if (!brokerTask.IsCompleted)
            {
                await brokerCancellation.CancelAsync();
                await brokerTask.WaitAsync(TimeSpan.FromSeconds(BrokerShutdownSeconds));
            }
        }
    }

    /// <summary>Unavailable, denied, duplicate, and invalid-duration broker paths all fail closed.</summary>
    /// <returns>A task that verifies broker denial paths.</returns>
    [Test]
    public async Task Broker_management_paths_fail_closed()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var inactiveScope = CreateUniqueScope(currentProcess.SessionId);
        var inactive = new WindowsUnattendedApprovalAuthorizer(inactiveScope);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(static () => new WindowsUnattendedApprovalAuthorizer(null!)).Throws<ArgumentNullException>();
        await Assert.That(await inactive.GetStatusAsync(CancellationToken.None)).IsNull();
        await Assert.That(await inactive.RevokeAsync(CancellationToken.None)).IsFalse();
        await Assert.That(
            await inactive.ValidateAsync(Guid.NewGuid(), CreateTarget(currentProcess), UiCapability.Inspect, CancellationToken.None)).IsFalse();
        await Assert.That(() => inactive.GetStatusAsync(cancellation.Token).AsTask()).Throws<OperationCanceledException>();

        var deniedAudit = new FakeAuditSink();
        using var denied = CreateBroker(
            currentProcess,
            inactiveScope,
            deniedAudit,
            static (_, _, _) => ValueTask.FromResult(false));
        await Assert.That(await denied.RunAsync(ApprovalHours, CancellationToken.None)).IsEqualTo(1);
        await Assert.That(ContainsAuditEvent(deniedAudit.Snapshot(), "unattended_approval_denied")).IsTrue();
        await Assert.That(() => denied.RunAsync(InvalidApprovalHours, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        var activeScope = CreateUniqueScope(currentProcess.SessionId);
        var firstAudit = new FakeAuditSink();
        var duplicateAudit = new FakeAuditSink();
        using var first = CreateBroker(
            currentProcess,
            activeScope,
            firstAudit,
            static (_, _, _) => ValueTask.FromResult(true));
        using var duplicate = CreateBroker(
            currentProcess,
            activeScope,
            duplicateAudit,
            static (_, _, _) => ValueTask.FromResult(true));
        using var firstCancellation = new CancellationTokenSource();
        var firstTask = Task.Run(() => first.RunAsync(ApprovalHours, firstCancellation.Token));
        var active = new WindowsUnattendedApprovalAuthorizer(activeScope);
        _ = await WaitForLeaseAsync(active);
        try
        {
            await Assert.That(await duplicate.RunAsync(ApprovalHours, CancellationToken.None)).IsEqualTo(DuplicateBrokerExitCode);
            await Assert.That(await active.RevokeAsync(CancellationToken.None)).IsTrue();
            await Assert.That(await firstTask.WaitAsync(TimeSpan.FromSeconds(BrokerShutdownSeconds))).IsEqualTo(0);
        }
        finally
        {
            _ = await active.RevokeAsync(CancellationToken.None);
            if (!firstTask.IsCompleted)
            {
                await firstCancellation.CancelAsync();
                await firstTask.WaitAsync(TimeSpan.FromSeconds(BrokerShutdownSeconds));
            }
        }
    }

    /// <summary>The broker cannot remain active beyond the expiry displayed before user approval.</summary>
    /// <returns>A task that verifies prompt time is included in the hard lease window.</returns>
    [Test]
    public async Task Approval_window_never_outlives_displayed_expiry()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var time = new FakeTimeProvider(DateTimeOffset.Parse(InitialUtcText));
        var audit = new FakeAuditSink();
        using var broker = new WindowsUnattendedApprovalBroker(
            time,
            new FakeProcessIdentityProvider { Current = CreateTarget(currentProcess), },
            CreateUniqueScope(currentProcess.SessionId),
            audit,
            (_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                time.Advance(TimeSpan.FromHours(ApprovalHours));
                return ValueTask.FromResult(true);
            });

        var result = await broker.RunAsync(ApprovalHours, CancellationToken.None);

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(ContainsAuditEvent(audit.Snapshot(), "unattended_approval_granted")).IsTrue();
        await Assert.That(ContainsAuditEvent(audit.Snapshot(), "unattended_approval_expired")).IsTrue();
    }

    /// <summary>Create a deterministic broker using a unique real named-pipe scope.</summary>
    /// <param name="process">Current test process.</param>
    /// <param name="scope">Unique transport scope.</param>
    /// <param name="audit">Captured audit sink.</param>
    /// <param name="showApproval">Deterministic approval decision.</param>
    /// <returns>Composed broker.</returns>
    private static WindowsUnattendedApprovalBroker CreateBroker(
        Process process,
        UnattendedApprovalScope scope,
        FakeAuditSink audit,
        Func<string, string, CancellationToken, ValueTask<bool>> showApproval)
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(InitialUtcText));
        return new(
            time,
            new FakeProcessIdentityProvider { Current = CreateTarget(process), },
            scope,
            audit,
            showApproval);
    }

    /// <summary>Create a stable fixture identity for the current process.</summary>
    /// <param name="process">Current process.</param>
    /// <returns>Fixture identity.</returns>
    private static ProcessIdentity CreateTarget(Process process) =>
        new(
            process.Id,
            DateTimeOffset.Parse("2026-07-27T11:00:00Z"),
            process.ProcessName,
            Environment.ProcessPath ?? string.Empty,
            process.SessionId);

    /// <summary>Send more simultaneous validations than the broker's active listener count.</summary>
    /// <param name="authorizer">Broker client.</param>
    /// <param name="lease">Active lease.</param>
    /// <param name="target">Exact target identity.</param>
    /// <returns>A task that verifies every validation result.</returns>
    private static async Task ValidateConcurrentlyAsync(
        WindowsUnattendedApprovalAuthorizer authorizer,
        UnattendedApprovalLease lease,
        ProcessIdentity target)
    {
        var validations = new Task<bool>[ConcurrentClients];
        for (var validationIndex = 0; validationIndex < validations.Length; validationIndex++)
        {
            validations[validationIndex] = authorizer
                .ValidateAsync(lease.LeaseId, target, UiCapability.Inspect, CancellationToken.None)
                .AsTask();
        }

        var results = await Task.WhenAll(validations);
        foreach (var result in results)
        {
            await Assert.That(result).IsTrue();
        }
    }

    /// <summary>Create a collision-free current-user named-pipe and process-lifetime marker scope.</summary>
    /// <param name="sessionId">Current interactive session.</param>
    /// <returns>Unique scope.</returns>
    private static UnattendedApprovalScope CreateUniqueScope(int sessionId)
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new(
            sessionId,
            $"UIInspect.MCP.Tests.{suffix}",
            $"Local\\UIInspect.MCP.Tests.{suffix}");
    }

    /// <summary>Wait until the approved broker has created its pipe listeners.</summary>
    /// <param name="authorizer">Broker client.</param>
    /// <returns>Active lease, or <see langword="null"/> after the bounded wait.</returns>
    private static async Task<UnattendedApprovalLease?> WaitForLeaseAsync(
        WindowsUnattendedApprovalAuthorizer authorizer)
    {
        for (var attempt = 0; attempt < StatusPollAttempts; attempt++)
        {
            var lease = await authorizer.GetStatusAsync(CancellationToken.None);
            if (lease is not null)
            {
                return lease;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(StatusPollMilliseconds));
        }

        return null;
    }

    /// <summary>Find one captured broker lifecycle event.</summary>
    /// <param name="events">Captured audit records.</param>
    /// <param name="eventType">Expected event type.</param>
    /// <returns><see langword="true"/> when the event is present.</returns>
    private static bool ContainsAuditEvent(IReadOnlyList<AuditEvent> events, string eventType)
    {
        foreach (var auditEvent in events)
        {
            if (string.Equals(auditEvent.EventType, eventType, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

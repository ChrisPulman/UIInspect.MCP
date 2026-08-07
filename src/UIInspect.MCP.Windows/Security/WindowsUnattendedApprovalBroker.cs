// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Auditing;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Windows.Processes;

namespace UIInspect.MCP.Windows.Security;

/// <summary>Owns one trusted unattended approval lease for the current Windows user/session.</summary>
public sealed class WindowsUnattendedApprovalBroker : IDisposable
{
    /// <summary>Maximum accepted request length.</summary>
    private const int MaximumRequestCharacters = 16 * 1024;

    /// <summary>Independent current-user-only listeners available to concurrent agent sessions.</summary>
    private const int ConcurrentBrokerListeners = 8;

    /// <summary>Exit code returned when another broker owns the user/session scope.</summary>
    private const int BrokerAlreadyRunningExitCode = 2;

    /// <summary>Exit code returned when the approval is denied or times out.</summary>
    private const int ApprovalDeniedExitCode = 1;

    /// <summary>Full unattended capability ceiling requested by the user.</summary>
    private const UiCapability AllCapabilities = UiCapability.Inspect | UiCapability.Interact | UiCapability.Keyboard;

    /// <summary>Maximum time to wait for the user to answer the trusted approval prompt.</summary>
    private static readonly TimeSpan ApprovalPromptTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Compact broker protocol serializer settings.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Current UTC clock.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Exact process identity resolver used independently by the broker.</summary>
    private readonly IProcessIdentityProvider _processes;

    /// <summary>Current Windows user/session scope.</summary>
    private readonly UnattendedApprovalScope _scope;

    /// <summary>Broker-owned append-only audit sink.</summary>
    private readonly IAuditSink _audit;

    /// <summary>Production audit resource owned by this broker, when present.</summary>
    private readonly JsonLineAuditSink? _ownedAudit;

    /// <summary>Trusted approval UI composed into the broker.</summary>
    private readonly Func<string, string, CancellationToken, ValueTask<bool>> _showApproval;

    /// <summary>Initializes a new instance of the <see cref="WindowsUnattendedApprovalBroker"/> class.</summary>
    public WindowsUnattendedApprovalBroker()
    {
        _timeProvider = TimeProvider.System;
        _processes = new WindowsProcessIdentityProvider();
        _scope = UnattendedApprovalScope.CreateCurrent();
        var audit = new JsonLineAuditSink(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UIInspect.MCP",
                "audit",
                "unattended-approval.jsonl"));
        _audit = audit;
        _ownedAudit = audit;
        _showApproval = TrustedWindowsDialog.ShowAsync;
    }

    /// <summary>Initializes a new instance of the <see cref="WindowsUnattendedApprovalBroker"/> class from composed collaborators.</summary>
    /// <param name="timeProvider">UTC clock.</param>
    /// <param name="processes">Exact process identity provider.</param>
    /// <param name="scope">Unique current-user/session transport scope.</param>
    /// <param name="audit">Redacted audit sink.</param>
    /// <param name="showApproval">Trusted approval UI.</param>
    internal WindowsUnattendedApprovalBroker(
        TimeProvider timeProvider,
        IProcessIdentityProvider processes,
        UnattendedApprovalScope scope,
        IAuditSink audit,
        Func<string, string, CancellationToken, ValueTask<bool>> showApproval)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _showApproval = showApproval ?? throw new ArgumentNullException(nameof(showApproval));
    }

    /// <summary>Prompt the local user and serve the resulting fixed-duration lease.</summary>
    /// <param name="hours">One of the supported approval windows.</param>
    /// <param name="cancellationToken">Process shutdown token.</param>
    /// <returns>Zero after a normal approval lifetime; non-zero when approval was denied or another broker owns the scope.</returns>
    public async Task<int> RunAsync(int hours, CancellationToken cancellationToken)
    {
        var duration = UnattendedApprovalDurations.FromHours(hours);
        using var brokerMarker = new EventWaitHandle(
            false,
            EventResetMode.ManualReset,
            _scope.MarkerName,
            out var ownsScope);
        if (!ownsScope)
        {
            return BrokerAlreadyRunningExitCode;
        }

        var lease = await RequestApprovalAsync(hours, duration, cancellationToken).ConfigureAwait(false);
        return lease is null
            ? ApprovalDeniedExitCode
            : await ServeLeaseAsync(lease, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _ownedAudit?.Dispose();

    /// <summary>Show the bounded trusted approval dialog.</summary>
    /// <param name="hours">Approval hours.</param>
    /// <param name="duration">Approval duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The approved lease, or <see langword="null"/>.</returns>
    private async ValueTask<UnattendedApprovalLease?> RequestApprovalAsync(
        int hours,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAtUtc = now.Add(duration);
        using var promptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        promptTimeout.CancelAfter(ApprovalPromptTimeout);
        try
        {
            var approved = await _showApproval(
                $"""
                Allow ALL local UIInspect MCP clients in this Windows sign-in session to inspect
                and interact with desktop applications for {hours} hour(s)?

                Capabilities: Inspect, Interact, Keyboard
                Scope: Current Windows user and interactive session {_scope.SessionId}
                Expires: {expiresAtUtc:O}

                UIInspect still verifies the exact PID, process start time, executable identity,
                and Windows session before every operation. This high-trust window is intended for
                unattended automated testing and can be revoked at any time with:

                uiinspect-mcp --revoke-unattended
                """,
                "UIInspect unattended approval",
                promptTimeout.Token).ConfigureAwait(false);
            if (approved)
            {
                var lease = new UnattendedApprovalLease(Guid.NewGuid(), AllCapabilities, now, expiresAtUtc);
                await AuditAsync("unattended_approval_granted", "allowed", null, cancellationToken).ConfigureAwait(false);
                return lease;
            }
        }
        catch (OperationCanceledException)
        {
            // A prompt timeout is a non-terminal, fail-closed denial.
        }

        await AuditAsync("unattended_approval_denied", "denied", "user_or_timeout", CancellationToken.None).ConfigureAwait(false);
        return null;
    }

    /// <summary>Serve an approved lease until expiry or revocation.</summary>
    /// <param name="lease">Approved lease.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Zero after normal broker completion.</returns>
    private async Task<int> ServeLeaseAsync(
        UnattendedApprovalLease lease,
        CancellationToken cancellationToken)
    {
        var remaining = lease.ExpiresAtUtc - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            await AuditAsync(
                "unattended_approval_expired",
                "completed",
                null,
                CancellationToken.None).ConfigureAwait(false);
            return 0;
        }

        var lifetime = new BrokerLeaseLifetime(remaining, cancellationToken);
        try
        {
            var listeners = new Task[ConcurrentBrokerListeners];
            for (var listenerIndex = 0; listenerIndex < listeners.Length; listenerIndex++)
            {
                listeners[listenerIndex] = ServeRequestsAsync(lease, lifetime);
            }

            await Task.WhenAll(listeners).ConfigureAwait(false);
            await AuditAsync(
                lifetime.IsRevoked ? "unattended_approval_revoked" : "unattended_approval_expired",
                "completed",
                null,
                CancellationToken.None).ConfigureAwait(false);
            return 0;
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    /// <summary>Serve sequential connections through one of the parallel broker listeners.</summary>
    /// <param name="lease">Current lease.</param>
    /// <param name="lifetime">Shared lease lifetime and revocation signal.</param>
    /// <returns>Completion after expiry, shutdown, or revocation.</returns>
    private async Task ServeRequestsAsync(
        UnattendedApprovalLease lease,
        BrokerLeaseLifetime lifetime)
    {
        while (!lifetime.Token.IsCancellationRequested)
        {
            var revoked = await ServeNextRequestOnDedicatedThreadAsync(lease, lifetime.Token).ConfigureAwait(false);
            if (revoked)
            {
                await lifetime.RevokeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Serve the next broker connection.</summary>
    /// <param name="lease">Current lease.</param>
    /// <param name="cancellationToken">Broker lifetime token.</param>
    /// <returns><see langword="true"/> when the lease was revoked.</returns>
    private async Task<bool> ServeNextRequestOnDedicatedThreadAsync(
        UnattendedApprovalLease lease,
        CancellationToken cancellationToken)
    {
        var accepted = await Task.Factory.StartNew(
            static state =>
            {
                var request = (BrokerAcceptState)state!;
                return request.Owner.AcceptRequest(request.CancellationToken);
            },
            new BrokerAcceptState(this, cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).ConfigureAwait(false);
        if (accepted is null)
        {
            return false;
        }

        await using (accepted.Pipe)
        await using (cancellationToken.Register(
            static state => ((NamedPipeServerStream)state!).Dispose(),
            accepted.Pipe))
        {
            try
            {
                return await HandleRequestAsync(
                    accepted.Pipe,
                    accepted.Line,
                    lease,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (IOException)
            {
                await AuditAsync(
                    "unattended_broker_request",
                    "failed",
                    "pipe_io",
                    CancellationToken.None).ConfigureAwait(false);
                return false;
            }
        }
    }

    /// <summary>Synchronously accept and read one bounded line on a dedicated listener thread.</summary>
    /// <param name="cancellationToken">Broker lifetime token.</param>
    /// <returns>A connected request, or <see langword="null"/> after cancellation.</returns>
    private AcceptedBrokerRequest? AcceptRequest(CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeServerStream(
            _scope.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var cancellationRegistration = cancellationToken.Register(
            static state => ((NamedPipeServerStream)state!).Dispose(),
            pipe);
        try
        {
            pipe.WaitForConnection();
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            return new(pipe, reader.ReadLine());
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            pipe.Dispose();
            return null;
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
            pipe.Dispose();
            return null;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    /// <summary>Handle one bounded broker request.</summary>
    /// <param name="pipe">Connected current-user-only pipe.</param>
    /// <param name="line">Bounded request line read by the dedicated listener.</param>
    /// <param name="lease">Broker-owned lease.</param>
    /// <param name="cancellationToken">Broker lifetime token.</param>
    /// <returns><see langword="true"/> when the request revoked the lease.</returns>
    private async ValueTask<bool> HandleRequestAsync(
        NamedPipeServerStream pipe,
        string? line,
        UnattendedApprovalLease lease,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true, };
        UnattendedApprovalBrokerResponse response;
        var revoke = false;
        if (string.IsNullOrWhiteSpace(line) || line.Length > MaximumRequestCharacters)
        {
            response = new(false, null, "invalid_request");
        }
        else
        {
            try
            {
                var request = JsonSerializer.Deserialize<UnattendedApprovalBrokerRequest>(line, JsonOptions);
                (response, revoke) = request is null
                    ? (new(false, null, "invalid_request"), false)
                    : await ExecuteRequestAsync(request, lease, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                response = new(false, null, "invalid_json");
            }
        }

        var serialized = JsonSerializer.Serialize(response, JsonOptions);
        await writer.WriteLineAsync(serialized.AsMemory(), cancellationToken).ConfigureAwait(false);
        return revoke;
    }

    /// <summary>Execute one validated broker operation.</summary>
    /// <param name="request">Parsed request.</param>
    /// <param name="lease">Current lease.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response and revocation flag.</returns>
    private async ValueTask<(UnattendedApprovalBrokerResponse Response, bool Revoke)> ExecuteRequestAsync(
        UnattendedApprovalBrokerRequest request,
        UnattendedApprovalLease lease,
        CancellationToken cancellationToken)
    {
        if (_timeProvider.GetUtcNow() >= lease.ExpiresAtUtc)
        {
            return (new(false, null, "expired"), false);
        }

        if (string.Equals(request.Operation, "status", StringComparison.Ordinal))
        {
            var allowed = (lease.Capabilities & request.Capabilities) == request.Capabilities;
            return (new(allowed, allowed ? lease : null, allowed ? null : "capability_denied"), false);
        }

        return string.Equals(request.Operation, "revoke", StringComparison.Ordinal)
            ? (new(true, null, null), true)
            : (await ValidateRequestAsync(request, lease, cancellationToken).ConfigureAwait(false), false);
    }

    /// <summary>Validate one exact-target request independently of the MCP server.</summary>
    /// <param name="request">Validation request.</param>
    /// <param name="lease">Current lease.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation response.</returns>
    private async ValueTask<UnattendedApprovalBrokerResponse> ValidateRequestAsync(
        UnattendedApprovalBrokerRequest request,
        UnattendedApprovalLease lease,
        CancellationToken cancellationToken)
    {
        if (request.LeaseId != lease.LeaseId
            || request.Target is null
            || (lease.Capabilities & request.Capabilities) != request.Capabilities)
        {
            return new(false, null, "invalid_authority");
        }

        var current = await _processes.ResolveAsync(request.Target.ProcessId, cancellationToken).ConfigureAwait(false);
        var valid = current is not null
            && current == request.Target
            && current.SessionId == _scope.SessionId;
        return new(valid, valid ? lease : null, valid ? null : "target_changed");
    }

    /// <summary>Write one redacted broker audit event.</summary>
    /// <param name="eventType">Event type.</param>
    /// <param name="outcome">Outcome.</param>
    /// <param name="reason">Safe reason code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audit completion.</returns>
    private ValueTask AuditAsync(
        string eventType,
        string outcome,
        string? reason,
        CancellationToken cancellationToken) =>
        _audit.WriteAsync(
            new(
                _timeProvider.GetUtcNow(),
                Guid.NewGuid().ToString("N"),
                eventType,
                outcome,
                AuditHash.Compute("windows-user-session-broker"),
                null,
                null,
                "unattended_approval",
                reason),
            cancellationToken);

    /// <summary>Coordinates expiry and revocation across concurrent pipe listeners.</summary>
    private sealed class BrokerLeaseLifetime
    {
        /// <summary>Linked cancellation source shared by all listeners.</summary>
        private readonly CancellationTokenSource _cancellation;

        /// <summary>Atomic revocation flag.</summary>
        private int _isRevoked;

        /// <summary>Initializes a new instance of the <see cref="BrokerLeaseLifetime"/> class.</summary>
        /// <param name="duration">Hard lease lifetime.</param>
        /// <param name="cancellationToken">Broker shutdown token.</param>
        internal BrokerLeaseLifetime(TimeSpan duration, CancellationToken cancellationToken)
        {
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellation.CancelAfter(duration);
        }

        /// <summary>Gets the shared cancellation token.</summary>
        internal CancellationToken Token => _cancellation.Token;

        /// <summary>Gets a value indicating whether an explicit revocation ended the lease.</summary>
        internal bool IsRevoked => Volatile.Read(ref _isRevoked) != 0;

        /// <summary>Release the linked cancellation source.</summary>
        internal void Dispose() => _cancellation.Dispose();

        /// <summary>Signal revocation to every active listener.</summary>
        /// <returns>Completion after cancellation callbacks finish.</returns>
        internal async ValueTask RevokeAsync()
        {
            _ = Interlocked.Exchange(ref _isRevoked, 1);
            await _cancellation.CancelAsync().ConfigureAwait(false);
        }
    }

    /// <summary>State passed to one dedicated broker accept thread.</summary>
    /// <param name="Owner">Broker that owns the pipe scope.</param>
    /// <param name="CancellationToken">Broker lifetime token.</param>
    private sealed record BrokerAcceptState(
        WindowsUnattendedApprovalBroker Owner,
        CancellationToken CancellationToken);

    /// <summary>Connected pipe and the request line read on its dedicated listener thread.</summary>
    /// <param name="Pipe">Connected current-user-only pipe.</param>
    /// <param name="Line">Request line, or <see langword="null"/> at end of stream.</param>
    private sealed record AcceptedBrokerRequest(NamedPipeServerStream Pipe, string? Line);
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Services;

namespace UIInspect.MCP.Tests;

/// <summary>Tests the consent-gated automation coordinator.</summary>
public sealed class UiInspectServiceTests
{
    /// <summary>Expected native fixture window handle.</summary>
    private const int WindowHandle = 42;

    /// <summary>Expected successful inspection depth.</summary>
    private const int InspectionDepth = 4;

    /// <summary>Expected successful inspection node limit.</summary>
    private const int InspectionNodeLimit = 100;

    /// <summary>Expected missing session error code.</summary>
    private const string SessionNotFoundCode = "session_not_found";

    /// <summary>Expected rate-limit error code.</summary>
    private const string RateLimitedCode = "rate_limited";

    /// <summary>Expected target-changed error code.</summary>
    private const string TargetChangedCode = "target_changed";

    /// <summary>Expected expired-consent error code.</summary>
    private const string ConsentExpiredCode = "consent_expired";

    /// <summary>Expected retry duration after the discovery rate limit is exceeded.</summary>
    private const int DiscoveryRetryMilliseconds = 2000;

    /// <summary>Expected retry duration after the action rate limit is exceeded.</summary>
    private const int ActionRetryMilliseconds = 5000;

    /// <summary>Consent rate-limit retry duration in seconds.</summary>
    private const int ConsentRetrySeconds = 3;

    /// <summary>Action rate-limit retry duration in seconds.</summary>
    private const int ActionRetrySeconds = 4;

    /// <summary>Maximum accepted UI tree depth.</summary>
    private const int MaximumTreeDepth = 12;

    /// <summary>Maximum accepted UI tree nodes.</summary>
    private const int MaximumTreeNodes = 1000;

    /// <summary>One more than the permitted UI tree depth.</summary>
    private const int TooDeepTreeDepth = MaximumTreeDepth + 1;

    /// <summary>One more than the permitted UI tree nodes.</summary>
    private const int TooManyTreeNodes = MaximumTreeNodes + 1;

    /// <summary>The complete happy path executes every MVP operation and audits it.</summary>
    /// <returns>A task that completes when the assertion sequence succeeds.</returns>
    [Test]
    public async Task Happy_path_exercises_every_operation()
    {
        await using var harness = new ServiceHarness();

        var discovery = await harness.Service.DiscoverAsync(ServiceHarness.ClientId, CancellationToken.None);
        var consent = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            true,
            true,
            ServiceHarness.ClientId,
            CancellationToken.None);
        var attach = await harness.Service.AttachAsync(
            harness.Target.ProcessId,
            WindowHandle,
            ServiceHarness.ClientId,
            CancellationToken.None);
        var sessionId = attach.Data!.SessionId;
        var inspect = await harness.Service.InspectAsync(sessionId, InspectionDepth, InspectionNodeLimit, ServiceHarness.ClientId, CancellationToken.None);
        var invoke = await harness.Service.InvokeAsync(sessionId, "e", ServiceHarness.ClientId, CancellationToken.None);
        var click = await harness.Service.ClickAsync(sessionId, "e", ServiceHarness.ClientId, CancellationToken.None);
        var value = await harness.Service.SetValueAsync(sessionId, "e", "secret", ServiceHarness.ClientId, CancellationToken.None);
        var select = await harness.Service.SelectAsync(sessionId, "e", ServiceHarness.ClientId, CancellationToken.None);
        var expand = await harness.Service.SetExpandedAsync(sessionId, "e", true, ServiceHarness.ClientId, CancellationToken.None);
        var collapse = await harness.Service.SetExpandedAsync(sessionId, "e", false, ServiceHarness.ClientId, CancellationToken.None);
        var key = await harness.Service.SendKeyAsync(sessionId, "e", "ENTER", ServiceHarness.ClientId, CancellationToken.None);
        var close = await harness.Service.CloseSessionAsync(sessionId, ServiceHarness.ClientId, CancellationToken.None);
        var closeAgain = await harness.Service.CloseSessionAsync(sessionId, ServiceHarness.ClientId, CancellationToken.None);

        await Assert.That(discovery.Data!.Count).IsEqualTo(1);
        await Assert.That(consent.Data!.Capabilities).IsEqualTo(
            UiCapability.Inspect | UiCapability.Interact | UiCapability.Keyboard);
        await Assert.That(harness.Prompt.Requests.Count).IsEqualTo(1);
        await Assert.That(attach.Data.WindowHandle).IsEqualTo(WindowHandle);
        await Assert.That(inspect.Data!.SessionId).IsEqualTo(sessionId);
        await Assert.That(invoke.Success).IsTrue();
        await Assert.That(click.Success).IsTrue();
        await Assert.That(value.Success).IsTrue();
        await Assert.That(select.Success).IsTrue();
        await Assert.That(expand.Data!.Action).IsEqualTo("expand");
        await Assert.That(collapse.Data!.Action).IsEqualTo("collapse");
        await Assert.That(key.Success).IsTrue();
        await Assert.That(close.Data).IsTrue();
        await Assert.That(closeAgain.Error!.Code).IsEqualTo(SessionNotFoundCode);
        await Assert.That(harness.Session.Calls).Contains("value:e:secret");
        await Assert.That(ContainsAuditEvent(harness.Audit.Events, "set_value", null)).IsTrue();
        await Assert.That(ContainsAuditEvent(harness.Audit.Events, null, "secret")).IsFalse();
    }

    /// <summary>Discovery and consent return stable failures for rate, target, denial, and TOCTOU cases.</summary>
    /// <returns>A task that completes when the assertion sequence succeeds.</returns>
    [Test]
    public async Task Discovery_and_consent_failures_are_safe()
    {
        await using var harness = new ServiceHarness();
        harness.RateLimiter.Decisions.Enqueue(new(false, TimeSpan.FromMilliseconds(DiscoveryRetryMilliseconds)));
        var limitedDiscovery = await harness.Service.DiscoverAsync(ServiceHarness.ClientId, CancellationToken.None);

        harness.Processes.Current = null;
        var missing = await harness.Service.RequestConsentAsync(1, false, false, ServiceHarness.ClientId, CancellationToken.None);

        harness.Processes.Current = harness.Target;
        harness.RateLimiter.Decisions.Enqueue(new(false, TimeSpan.FromSeconds(ConsentRetrySeconds)));
        var limitedConsent = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            false,
            false,
            ServiceHarness.ClientId,
            CancellationToken.None);

        harness.Prompt.Approved = false;
        var denied = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            false,
            false,
            ServiceHarness.ClientId,
            CancellationToken.None);

        harness.Prompt.Approved = true;
        harness.Processes.Responses.Enqueue(harness.Target);
        harness.Processes.Responses.Enqueue(harness.Target with { StartedAtUtc = harness.Target.StartedAtUtc.AddSeconds(1) });
        var changed = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            false,
            false,
            ServiceHarness.ClientId,
            CancellationToken.None);

        await Assert.That(limitedDiscovery.Error!.Code).IsEqualTo(RateLimitedCode);
        await Assert.That(limitedDiscovery.Error.RetryAfterMilliseconds).IsEqualTo(DiscoveryRetryMilliseconds);
        await Assert.That(missing.Error!.Code).IsEqualTo("target_unavailable");
        await Assert.That(limitedConsent.Error!.Code).IsEqualTo(RateLimitedCode);
        await Assert.That(denied.Error!.Code).IsEqualTo("consent_denied");
        await Assert.That(changed.Error!.Code).IsEqualTo(TargetChangedCode);
    }

    /// <summary>Attach rejects unavailable, unconsented, and changed process instances.</summary>
    /// <returns>A task that completes when the assertion sequence succeeds.</returns>
    [Test]
    public async Task Attach_requires_current_consent()
    {
        await using var harness = new ServiceHarness();
        harness.Processes.Current = null;
        var unavailable = await harness.Service.AttachAsync(1, null, ServiceHarness.ClientId, CancellationToken.None);

        harness.Processes.Current = harness.Target;
        var unconsented = await harness.Service.AttachAsync(
            harness.Target.ProcessId,
            null,
            ServiceHarness.ClientId,
            CancellationToken.None);

        _ = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            true,
            false,
            ServiceHarness.ClientId,
            CancellationToken.None);
        harness.Backend.Session = new(
            harness.Target with { StartedAtUtc = harness.Target.StartedAtUtc.AddSeconds(1) });
        var changed = await harness.Service.AttachAsync(
            harness.Target.ProcessId,
            null,
            ServiceHarness.ClientId,
            CancellationToken.None);

        await Assert.That(unavailable.Error!.Code).IsEqualTo("target_unavailable");
        await Assert.That(unconsented.Error!.Code).IsEqualTo("consent_required");
        await Assert.That(changed.Error!.Code).IsEqualTo(TargetChangedCode);
        await Assert.That(harness.Backend.Session.DisposeCalls).IsEqualTo(1);
    }

    /// <summary>Inspection validates bounds, session ownership, target liveness, consent, and rates.</summary>
    /// <returns>A task that completes when the assertion sequence succeeds.</returns>
    [Test]
    public async Task Inspection_authorization_covers_all_denials()
    {
        await using var harness = new ServiceHarness();
        var invalidDepthLow = await harness.Service.InspectAsync("s", -1, 1, ServiceHarness.ClientId, CancellationToken.None);
        var invalidDepthHigh = await harness.Service.InspectAsync("s", TooDeepTreeDepth, 1, ServiceHarness.ClientId, CancellationToken.None);
        var invalidNodesLow = await harness.Service.InspectAsync("s", 1, 0, ServiceHarness.ClientId, CancellationToken.None);
        var invalidNodesHigh = await harness.Service.InspectAsync("s", 1, TooManyTreeNodes, ServiceHarness.ClientId, CancellationToken.None);
        var missing = await harness.Service.InspectAsync("missing", 1, 1, ServiceHarness.ClientId, CancellationToken.None);
        var sessionId = await harness.AttachWithConsentAsync();
        var wrongClient = await harness.Service.InspectAsync(sessionId, 1, 1, "other-client", CancellationToken.None);

        harness.Processes.Current = harness.Target with { StartedAtUtc = harness.Target.StartedAtUtc.AddSeconds(1) };
        var changed = await harness.Service.InspectAsync(sessionId, 1, 1, ServiceHarness.ClientId, CancellationToken.None);

        await Assert.That(invalidDepthLow.Error!.Code).IsEqualTo("invalid_depth");
        await Assert.That(invalidDepthHigh.Error!.Code).IsEqualTo("invalid_depth");
        await Assert.That(invalidNodesLow.Error!.Code).IsEqualTo("invalid_node_limit");
        await Assert.That(invalidNodesHigh.Error!.Code).IsEqualTo("invalid_node_limit");
        await Assert.That(missing.Error!.Code).IsEqualTo(SessionNotFoundCode);
        await Assert.That(wrongClient.Error!.Code).IsEqualTo(SessionNotFoundCode);
        await Assert.That(changed.Error!.Code).IsEqualTo(TargetChangedCode);
        await Assert.That(harness.Session.DisposeCalls).IsEqualTo(1);
    }

    /// <summary>Expired consent and rate limits deny an otherwise live attached session.</summary>
    /// <returns>A task that completes when the assertion sequence succeeds.</returns>
    [Test]
    public async Task Attached_session_honors_expiry_and_rate_limits()
    {
        var options = new UiInspectOptions
        {
            ConsentDuration = TimeSpan.FromSeconds(1),
            DiscoveryRatePerMinute = 1,
            InspectionRatePerMinute = 1,
            ActionRatePerMinute = 1,
            ConsentPromptRatePerMinute = 1,
        };
        await using var expiredHarness = new ServiceHarness(options);
        var expiredSession = await expiredHarness.AttachWithConsentAsync();
        expiredHarness.Time.Advance(TimeSpan.FromSeconds(1));
        var expired = await expiredHarness.Service.InspectAsync(
            expiredSession,
            1,
            1,
            ServiceHarness.ClientId,
            CancellationToken.None);

        await using var limitedHarness = new ServiceHarness();
        var limitedSession = await limitedHarness.AttachWithConsentAsync();
        limitedHarness.RateLimiter.Decisions.Enqueue(new(false, TimeSpan.FromMilliseconds(ActionRetryMilliseconds)));
        var limited = await limitedHarness.Service.InspectAsync(
            limitedSession,
            1,
            1,
            ServiceHarness.ClientId,
            CancellationToken.None);

        await Assert.That(expired.Error!.Code).IsEqualTo(ConsentExpiredCode);
        await Assert.That(expiredHarness.Session.DisposeCalls).IsEqualTo(1);
        await Assert.That(ContainsAuditEvent(expiredHarness.Audit.Events, null, ConsentExpiredCode)).IsTrue();
        await Assert.That(limited.Error!.Code).IsEqualTo(RateLimitedCode);
        await Assert.That(limited.Error.RetryAfterMilliseconds).IsEqualTo(ActionRetryMilliseconds);
        await Assert.That(ContainsAuditEvent(limitedHarness.Audit.Events, null, RateLimitedCode)).IsTrue();
    }

    /// <summary>Actions validate arguments, capability, provider errors, and rate limits.</summary>
    /// <returns>A task that completes when the assertion sequence succeeds.</returns>
    [Test]
    public async Task Action_failures_are_structured()
    {
        await using var readOnlyHarness = new ServiceHarness();
        var readOnlySession = await readOnlyHarness.AttachWithConsentAsync(false, false);
        var noActionConsent = await readOnlyHarness.Service.InvokeAsync(
            readOnlySession,
            "e",
            ServiceHarness.ClientId,
            CancellationToken.None);
        var noKeyboardConsent = await readOnlyHarness.Service.SendKeyAsync(
            readOnlySession,
            "e",
            "ENTER",
            ServiceHarness.ClientId,
            CancellationToken.None);

        await using var harness = new ServiceHarness();
        var sessionId = await harness.AttachWithConsentAsync();
        harness.Session.Results.Enqueue(PlatformActionResult.Fail("pattern_not_supported", "unsupported"));
        var providerFailure = await harness.Service.InvokeAsync(sessionId, "e", ServiceHarness.ClientId, CancellationToken.None);
        harness.Session.Results.Enqueue(new(false, null, "unspecified provider failure"));
        var unspecifiedProviderFailure = await harness.Service.InvokeAsync(
            sessionId,
            "e",
            ServiceHarness.ClientId,
            CancellationToken.None);
        harness.RateLimiter.Decisions.Enqueue(new(false, TimeSpan.FromSeconds(ActionRetrySeconds)));
        var limited = await harness.Service.ClickAsync(sessionId, "e", ServiceHarness.ClientId, CancellationToken.None);

        await Assert.That(noActionConsent.Error!.Code).IsEqualTo(ConsentExpiredCode);
        await Assert.That(noKeyboardConsent.Error!.Code).IsEqualTo(SessionNotFoundCode);
        await Assert.That(providerFailure.Error!.Code).IsEqualTo("pattern_not_supported");
        await Assert.That(unspecifiedProviderFailure.Error!.Code).IsEqualTo("automation_failed");
        await Assert.That(limited.Error!.Code).IsEqualTo(RateLimitedCode);
        await Assert.That(async () => { _ = await harness.Service.InvokeAsync(sessionId, " ", ServiceHarness.ClientId, CancellationToken.None); }).Throws<ArgumentException>();
        await Assert.That(async () => { _ = await harness.Service.SetValueAsync(sessionId, "e", null!, ServiceHarness.ClientId, CancellationToken.None); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await harness.Service.SendKeyAsync(sessionId, "e", " ", ServiceHarness.ClientId, CancellationToken.None); }).Throws<ArgumentException>();
    }

    /// <summary>Constructor dependencies and unsafe option values fail fast.</summary>
    /// <returns>A task that completes when the assertion sequence succeeds.</returns>
    [Test]
    public async Task Constructor_validates_dependencies_and_options()
    {
        await using var harness = new ServiceHarness();

        UiInspectServiceDependencies CreateDependencies() =>
            new(
                harness.Backend,
                harness.Processes,
                harness.Prompt,
                harness.Consent,
                harness.RateLimiter,
                harness.Audit,
                harness.Time);

        await Assert.That(() => new UiInspectService(null!, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies() with { Backend = null! }, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies() with { Processes = null! }, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies() with { ConsentPrompt = null! }, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies() with { ConsentRegistry = null! }, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies() with { RateLimiter = null! }, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies() with { AuditSink = null! }, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies() with { TimeProvider = null! }, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(CreateDependencies(), null!)).Throws<ArgumentNullException>();

        var invalidOptions = new[]
        {
            new UiInspectOptions { ConsentDuration = TimeSpan.Zero },
            new UiInspectOptions { MaximumTreeDepth = -1 },
            new UiInspectOptions { MaximumTreeNodes = 0 },
            new UiInspectOptions { DiscoveryRatePerMinute = 0 },
            new UiInspectOptions { InspectionRatePerMinute = 0 },
            new UiInspectOptions { ActionRatePerMinute = 0 },
            new UiInspectOptions { ConsentPromptRatePerMinute = 0 },
        };
        foreach (var options in invalidOptions)
        {
            await Assert.That(() => new UiInspectService(CreateDependencies(), options)).Throws<ArgumentOutOfRangeException>();
        }
    }

    /// <summary>Checks whether a captured audit event matches the provided operation or reason.</summary>
    /// <param name="events">Captured audit events.</param>
    /// <param name="operation">Optional expected operation.</param>
    /// <param name="reasonFragment">Optional expected reason fragment.</param>
    /// <returns><see langword="true"/> when a matching event exists.</returns>
    private static bool ContainsAuditEvent(IReadOnlyList<AuditEvent> events, string? operation, string? reasonFragment)
    {
        foreach (var auditEvent in events)
        {
            var hasOperation = operation is null || auditEvent.Operation == operation;
            var hasReason = reasonFragment is null || auditEvent.Reason?.Contains(reasonFragment, StringComparison.Ordinal) is true;
            if (hasOperation && hasReason)
            {
                return true;
            }
        }

        return false;
    }
}

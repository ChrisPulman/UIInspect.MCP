// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Core.Services;

namespace UIInspect.MCP.Tests;

/// <summary>Tests the consent-gated automation coordinator.</summary>
public sealed class UiInspectServiceTests
{
    /// <summary>The complete happy path executes every MVP operation and audits it.</summary>
    [Test]
    public async Task Happy_path_exercises_every_operation()
    {
        await using var harness = new ServiceHarness();

        var discovery = await harness.Service.DiscoverAsync(ServiceHarness.ClientId);
        var consent = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            true,
            true,
            ServiceHarness.ClientId);
        var attach = await harness.Service.AttachAsync(
            harness.Target.ProcessId,
            42,
            ServiceHarness.ClientId);
        var sessionId = attach.Data!.SessionId;
        var inspect = await harness.Service.InspectAsync(sessionId, 4, 100, ServiceHarness.ClientId);
        var invoke = await harness.Service.InvokeAsync(sessionId, "e", ServiceHarness.ClientId);
        var click = await harness.Service.ClickAsync(sessionId, "e", ServiceHarness.ClientId);
        var value = await harness.Service.SetValueAsync(sessionId, "e", "secret", ServiceHarness.ClientId);
        var select = await harness.Service.SelectAsync(sessionId, "e", ServiceHarness.ClientId);
        var expand = await harness.Service.SetExpandedAsync(sessionId, "e", true, ServiceHarness.ClientId);
        var collapse = await harness.Service.SetExpandedAsync(sessionId, "e", false, ServiceHarness.ClientId);
        var key = await harness.Service.SendKeyAsync(sessionId, "e", "ENTER", ServiceHarness.ClientId);
        var close = await harness.Service.CloseSessionAsync(sessionId, ServiceHarness.ClientId);
        var closeAgain = await harness.Service.CloseSessionAsync(sessionId, ServiceHarness.ClientId);

        await Assert.That(discovery.Data!.Count).IsEqualTo(1);
        await Assert.That(consent.Data!.Capabilities).IsEqualTo(
            UiCapability.Inspect | UiCapability.Interact | UiCapability.Keyboard);
        await Assert.That(harness.Prompt.Requests.Count).IsEqualTo(1);
        await Assert.That(attach.Data.WindowHandle).IsEqualTo(42);
        await Assert.That(inspect.Data!.SessionId).IsEqualTo(sessionId);
        await Assert.That(invoke.Success).IsTrue();
        await Assert.That(click.Success).IsTrue();
        await Assert.That(value.Success).IsTrue();
        await Assert.That(select.Success).IsTrue();
        await Assert.That(expand.Data!.Action).IsEqualTo("expand");
        await Assert.That(collapse.Data!.Action).IsEqualTo("collapse");
        await Assert.That(key.Success).IsTrue();
        await Assert.That(close.Data).IsTrue();
        await Assert.That(closeAgain.Error!.Code).IsEqualTo("session_not_found");
        await Assert.That(harness.Session.Calls).Contains("value:e:secret");
        await Assert.That(harness.Audit.Events.Any(item => item.Operation == "set_value")).IsTrue();
        await Assert.That(harness.Audit.Events.Any(item => item.Reason?.Contains("secret", StringComparison.Ordinal) is true)).IsFalse();
    }

    /// <summary>Discovery and consent return stable failures for rate, target, denial, and TOCTOU cases.</summary>
    [Test]
    public async Task Discovery_and_consent_failures_are_safe()
    {
        await using var harness = new ServiceHarness();
        harness.RateLimiter.Decisions.Enqueue(new RateLimitDecision(false, TimeSpan.FromSeconds(2)));
        var limitedDiscovery = await harness.Service.DiscoverAsync(ServiceHarness.ClientId);

        harness.Processes.Current = null;
        var missing = await harness.Service.RequestConsentAsync(1, false, false, ServiceHarness.ClientId);

        harness.Processes.Current = harness.Target;
        harness.RateLimiter.Decisions.Enqueue(new RateLimitDecision(false, TimeSpan.FromSeconds(3)));
        var limitedConsent = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            false,
            false,
            ServiceHarness.ClientId);

        harness.Prompt.Approved = false;
        var denied = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            false,
            false,
            ServiceHarness.ClientId);

        harness.Prompt.Approved = true;
        harness.Processes.Responses.Enqueue(harness.Target);
        harness.Processes.Responses.Enqueue(harness.Target with { StartedAtUtc = harness.Target.StartedAtUtc.AddSeconds(1) });
        var changed = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            false,
            false,
            ServiceHarness.ClientId);

        await Assert.That(limitedDiscovery.Error!.Code).IsEqualTo("rate_limited");
        await Assert.That(limitedDiscovery.Error.RetryAfterMilliseconds).IsEqualTo(2000);
        await Assert.That(missing.Error!.Code).IsEqualTo("target_unavailable");
        await Assert.That(limitedConsent.Error!.Code).IsEqualTo("rate_limited");
        await Assert.That(denied.Error!.Code).IsEqualTo("consent_denied");
        await Assert.That(changed.Error!.Code).IsEqualTo("target_changed");
    }

    /// <summary>Attach rejects unavailable, unconsented, and changed process instances.</summary>
    [Test]
    public async Task Attach_requires_current_consent()
    {
        await using var harness = new ServiceHarness();
        harness.Processes.Current = null;
        var unavailable = await harness.Service.AttachAsync(1, null, ServiceHarness.ClientId);

        harness.Processes.Current = harness.Target;
        var unconsented = await harness.Service.AttachAsync(
            harness.Target.ProcessId,
            null,
            ServiceHarness.ClientId);

        _ = await harness.Service.RequestConsentAsync(
            harness.Target.ProcessId,
            true,
            false,
            ServiceHarness.ClientId);
        harness.Backend.Session = new FakeUiAutomationSession(
            harness.Target with { StartedAtUtc = harness.Target.StartedAtUtc.AddSeconds(1) });
        var changed = await harness.Service.AttachAsync(
            harness.Target.ProcessId,
            null,
            ServiceHarness.ClientId);

        await Assert.That(unavailable.Error!.Code).IsEqualTo("target_unavailable");
        await Assert.That(unconsented.Error!.Code).IsEqualTo("consent_required");
        await Assert.That(changed.Error!.Code).IsEqualTo("target_changed");
        await Assert.That(harness.Backend.Session.DisposeCalls).IsEqualTo(1);
    }

    /// <summary>Inspection validates bounds, session ownership, target liveness, consent, and rates.</summary>
    [Test]
    public async Task Inspection_authorization_covers_all_denials()
    {
        await using var harness = new ServiceHarness();
        var invalidDepthLow = await harness.Service.InspectAsync("s", -1, 1, ServiceHarness.ClientId);
        var invalidDepthHigh = await harness.Service.InspectAsync("s", 13, 1, ServiceHarness.ClientId);
        var invalidNodesLow = await harness.Service.InspectAsync("s", 1, 0, ServiceHarness.ClientId);
        var invalidNodesHigh = await harness.Service.InspectAsync("s", 1, 1001, ServiceHarness.ClientId);
        var missing = await harness.Service.InspectAsync("missing", 1, 1, ServiceHarness.ClientId);
        var sessionId = await harness.AttachWithConsentAsync();
        var wrongClient = await harness.Service.InspectAsync(sessionId, 1, 1, "other-client");

        harness.Processes.Current = harness.Target with { StartedAtUtc = harness.Target.StartedAtUtc.AddSeconds(1) };
        var changed = await harness.Service.InspectAsync(sessionId, 1, 1, ServiceHarness.ClientId);

        await Assert.That(invalidDepthLow.Error!.Code).IsEqualTo("invalid_depth");
        await Assert.That(invalidDepthHigh.Error!.Code).IsEqualTo("invalid_depth");
        await Assert.That(invalidNodesLow.Error!.Code).IsEqualTo("invalid_node_limit");
        await Assert.That(invalidNodesHigh.Error!.Code).IsEqualTo("invalid_node_limit");
        await Assert.That(missing.Error!.Code).IsEqualTo("session_not_found");
        await Assert.That(wrongClient.Error!.Code).IsEqualTo("session_not_found");
        await Assert.That(changed.Error!.Code).IsEqualTo("target_changed");
        await Assert.That(harness.Session.DisposeCalls).IsEqualTo(1);
    }

    /// <summary>Expired consent and rate limits deny an otherwise live attached session.</summary>
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
            ServiceHarness.ClientId);

        await using var limitedHarness = new ServiceHarness();
        var limitedSession = await limitedHarness.AttachWithConsentAsync();
        limitedHarness.RateLimiter.Decisions.Enqueue(new RateLimitDecision(false, TimeSpan.FromSeconds(5)));
        var limited = await limitedHarness.Service.InspectAsync(
            limitedSession,
            1,
            1,
            ServiceHarness.ClientId);

        await Assert.That(expired.Error!.Code).IsEqualTo("consent_expired");
        await Assert.That(limited.Error!.Code).IsEqualTo("rate_limited");
        await Assert.That(limited.Error.RetryAfterMilliseconds).IsEqualTo(5000);
    }

    /// <summary>Actions validate arguments, capability, provider errors, and rate limits.</summary>
    [Test]
    public async Task Action_failures_are_structured()
    {
        await using var readOnlyHarness = new ServiceHarness();
        var readOnlySession = await readOnlyHarness.AttachWithConsentAsync(false, false);
        var noActionConsent = await readOnlyHarness.Service.InvokeAsync(
            readOnlySession,
            "e",
            ServiceHarness.ClientId);
        var noKeyboardConsent = await readOnlyHarness.Service.SendKeyAsync(
            readOnlySession,
            "e",
            "ENTER",
            ServiceHarness.ClientId);

        await using var harness = new ServiceHarness();
        var sessionId = await harness.AttachWithConsentAsync();
        harness.Session.Results.Enqueue(PlatformActionResult.Fail("pattern_not_supported", "unsupported"));
        var providerFailure = await harness.Service.InvokeAsync(sessionId, "e", ServiceHarness.ClientId);
        harness.Session.Results.Enqueue(new PlatformActionResult(false, null, "unspecified provider failure"));
        var unspecifiedProviderFailure = await harness.Service.InvokeAsync(
            sessionId,
            "e",
            ServiceHarness.ClientId);
        harness.RateLimiter.Decisions.Enqueue(new RateLimitDecision(false, TimeSpan.FromSeconds(4)));
        var limited = await harness.Service.ClickAsync(sessionId, "e", ServiceHarness.ClientId);

        await Assert.That(noActionConsent.Error!.Code).IsEqualTo("consent_expired");
        await Assert.That(noKeyboardConsent.Error!.Code).IsEqualTo("consent_expired");
        await Assert.That(providerFailure.Error!.Code).IsEqualTo("pattern_not_supported");
        await Assert.That(unspecifiedProviderFailure.Error!.Code).IsEqualTo("automation_failed");
        await Assert.That(limited.Error!.Code).IsEqualTo("rate_limited");
        await Assert.That(async () => { _ = await harness.Service.InvokeAsync(sessionId, " ", ServiceHarness.ClientId); }).Throws<ArgumentException>();
        await Assert.That(async () => { _ = await harness.Service.SetValueAsync(sessionId, "e", null!, ServiceHarness.ClientId); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await harness.Service.SendKeyAsync(sessionId, "e", " ", ServiceHarness.ClientId); }).Throws<ArgumentException>();
    }

    /// <summary>Constructor dependencies and unsafe option values fail fast.</summary>
    [Test]
    public async Task Constructor_validates_dependencies_and_options()
    {
        await using var harness = new ServiceHarness();

        UiInspectService Create(
            IUiAutomationBackend? backend = null,
            IProcessIdentityProvider? processes = null,
            IUserConsentPrompt? prompt = null,
            ConsentRegistry? consent = null,
            IOperationRateLimiter? limiter = null,
            IAuditSink? audit = null,
            TimeProvider? time = null,
            UiInspectOptions? options = null) =>
            new(
                backend ?? harness.Backend,
                processes ?? harness.Processes,
                prompt ?? harness.Prompt,
                consent ?? harness.Consent,
                limiter ?? harness.RateLimiter,
                audit ?? harness.Audit,
                time ?? harness.Time,
                options ?? harness.Options);

        await Assert.That(() => new UiInspectService(null!, harness.Processes, harness.Prompt, harness.Consent, harness.RateLimiter, harness.Audit, harness.Time, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(harness.Backend, null!, harness.Prompt, harness.Consent, harness.RateLimiter, harness.Audit, harness.Time, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(harness.Backend, harness.Processes, null!, harness.Consent, harness.RateLimiter, harness.Audit, harness.Time, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(harness.Backend, harness.Processes, harness.Prompt, null!, harness.RateLimiter, harness.Audit, harness.Time, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(harness.Backend, harness.Processes, harness.Prompt, harness.Consent, null!, harness.Audit, harness.Time, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(harness.Backend, harness.Processes, harness.Prompt, harness.Consent, harness.RateLimiter, null!, harness.Time, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(harness.Backend, harness.Processes, harness.Prompt, harness.Consent, harness.RateLimiter, harness.Audit, null!, harness.Options)).Throws<ArgumentNullException>();
        await Assert.That(() => new UiInspectService(harness.Backend, harness.Processes, harness.Prompt, harness.Consent, harness.RateLimiter, harness.Audit, harness.Time, null!)).Throws<ArgumentNullException>();

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
            await Assert.That(() => Create(options: options)).Throws<ArgumentOutOfRangeException>();
        }
    }
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text.Json;
using UIInspect.MCP.Core.Auditing;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Windows.Automation;
using UIInspect.MCP.Windows.Processes;

namespace UIInspect.MCP.Tests;

/// <summary>Tests pure models, consent, rate limiting, audit persistence, and process identity.</summary>
public sealed class CorePolicyTests
{
    /// <summary>The fixed client identity used by policy tests.</summary>
    private const string ClientId = "client";

    /// <summary>The start instant used by deterministic policy tests.</summary>
    private const string InitialUtcText = "2026-07-27T12:00:00Z";

    /// <summary>The ordinal assigned to the third selector.</summary>
    private const int ThirdOrdinal = 2;

    /// <summary>The expected selector segment count.</summary>
    private const int ExpectedSegmentCount = 3;

    /// <summary>The expected SHA-256 hexadecimal length.</summary>
    private const int ExpectedAuditHashLength = 64;

    /// <summary>The test delay in milliseconds.</summary>
    private const int TestDelayMilliseconds = 25;

    /// <summary>The process identifier used by consent tests.</summary>
    private const int TestProcessId = 10;

    /// <summary>The number of grants used by rate limiter tests.</summary>
    private const int RateLimit = 2;

    /// <summary>The single-grant limit for an independent bucket.</summary>
    private const int SingleGrantLimit = 1;

    /// <summary>The expected number of JSON lines in the audit file.</summary>
    private const int ExpectedAuditLineCount = 2;

    /// <summary>Result factories preserve success and safe error data.</summary>
    /// <returns>A task that verifies result factories.</returns>
    [Test]
    public async Task Result_factories_are_consistent()
    {
        var ok = UiResult<string>.Ok("value");
        var failed = UiResult<string>.Fail("rate_limited", "wait", TimeSpan.FromMilliseconds(TestDelayMilliseconds));
        var platformOk = PlatformActionResult.Ok("done");
        var platformFailure = PlatformActionResult.Fail("failed", "no");

        await Assert.That(ok.Success).IsTrue();
        await Assert.That(ok.Data).IsEqualTo("value");
        await Assert.That(ok.Error).IsNull();
        await Assert.That(failed.Success).IsFalse();
        await Assert.That(failed.Data).IsNull();
        await Assert.That(failed.Error!.RetryAfterMilliseconds).IsEqualTo(TestDelayMilliseconds);
        await Assert.That(platformOk.Succeeded).IsTrue();
        await Assert.That(platformFailure.ErrorCode).IsEqualTo("failed");
    }

    /// <summary>Audit hashing is deterministic and rejects blank identities.</summary>
    /// <returns>A task that verifies audit hashes.</returns>
    [Test]
    public async Task Audit_hash_is_safe_and_deterministic()
    {
        var first = AuditHash.Compute(ClientId);
        var second = AuditHash.Compute(ClientId);

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first.Length).IsEqualTo(ExpectedAuditHashLength);
        await Assert.That(first).DoesNotContain(ClientId);
        await Assert.That(static () => AuditHash.Compute(" ")).Throws<ArgumentException>();
    }

    /// <summary>Consent is scoped, expiring, revocable, and bound to an exact target.</summary>
    /// <returns>A task that verifies consent policy.</returns>
    [Test]
    public async Task Consent_registry_enforces_scope_expiry_and_revocation()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(InitialUtcText));
        var registry = new ConsentRegistry(time);
        var target = new ProcessIdentity(TestProcessId, time.UtcNow, "app", "app.exe", 1);
        var other = target with { StartedAtUtc = target.StartedAtUtc.AddSeconds(1) };
        var first = registry.Grant(ClientId, target, UiCapability.Inspect, TimeSpan.FromMinutes(1));
        _ = registry.Grant(ClientId, other, UiCapability.Interact, TimeSpan.FromMinutes(RateLimit));

        await Assert.That(registry.FindActive(ClientId, target, UiCapability.Inspect)).IsEqualTo(first);
        await Assert.That(registry.FindActive("other-client", target, UiCapability.Inspect)).IsNull();
        await Assert.That(registry.FindActive(ClientId, target, UiCapability.Interact)).IsNull();
        await Assert.That(registry.Revoke(Guid.NewGuid())).IsFalse();
        await Assert.That(registry.Revoke(first.Id)).IsTrue();
        await Assert.That(registry.FindActive(ClientId, target, UiCapability.Inspect)).IsNull();
        await Assert.That(registry.RevokeTarget(target)).IsEqualTo(0);
        await Assert.That(registry.RevokeTarget(other)).IsEqualTo(1);

        var expiring = registry.Grant(ClientId, target, UiCapability.Inspect, TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(1));
        await Assert.That(registry.FindActive(ClientId, target, UiCapability.Inspect)).IsNull();
        await Assert.That(registry.Revoke(expiring.Id)).IsFalse();

        await Assert.That(static () => new ConsentRegistry(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => registry.Grant(" ", target, UiCapability.Inspect, TimeSpan.FromSeconds(1))).Throws<ArgumentException>();
        await Assert.That(() => registry.Grant(ClientId, null!, UiCapability.Inspect, TimeSpan.FromSeconds(1))).Throws<ArgumentNullException>();
        await Assert.That(() => registry.Grant(ClientId, target, UiCapability.Inspect, TimeSpan.Zero)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>The session prompt validates inputs before reserving or displaying a decision.</summary>
    /// <returns>A task that verifies prompt input validation.</returns>
    [Test]
    public async Task Session_consent_prompt_validates_inputs()
    {
        var prompt = new SessionUserConsentPrompt(
            new FakeConsentPrompt(),
            new FakeRateLimiter(),
            new UiInspectOptions());
        var target = new ProcessIdentity(TestProcessId, DateTimeOffset.Parse(InitialUtcText), "app", "app.exe", 1);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => { _ = await prompt.RequestAsync(null!, UiCapability.Inspect, ClientId, CancellationToken.None); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await prompt.RequestAsync(target, UiCapability.Inspect, " ", CancellationToken.None); }).Throws<ArgumentException>();
        await Assert.That(async () => { _ = await prompt.RequestAsync(target, UiCapability.Inspect, ClientId, cancellation.Token); }).Throws<OperationCanceledException>();
    }

    /// <summary>The fixed-window limiter resets on time and separates buckets.</summary>
    /// <returns>A task that verifies rate-limiter behavior.</returns>
    [Test]
    public async Task Fixed_window_rate_limiter_is_deterministic()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse(InitialUtcText));
        var limiter = new FixedWindowRateLimiter(time);

        await Assert.That(limiter.TryAcquire("a", RateLimit, TimeSpan.FromSeconds(TestProcessId)).IsAllowed).IsTrue();
        await Assert.That(limiter.TryAcquire("a", RateLimit, TimeSpan.FromSeconds(TestProcessId)).IsAllowed).IsTrue();
        var blocked = limiter.TryAcquire("a", RateLimit, TimeSpan.FromSeconds(TestProcessId));
        await Assert.That(blocked.IsAllowed).IsFalse();
        await Assert.That(blocked.RetryAfter).IsEqualTo(TimeSpan.FromSeconds(TestProcessId));
        await Assert.That(limiter.TryAcquire("b", SingleGrantLimit, TimeSpan.FromSeconds(TestProcessId)).IsAllowed).IsTrue();
        time.Advance(TimeSpan.FromSeconds(TestProcessId));
        await Assert.That(limiter.TryAcquire("a", RateLimit, TimeSpan.FromSeconds(TestProcessId)).IsAllowed).IsTrue();

        await Assert.That(static () => new FixedWindowRateLimiter(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => limiter.TryAcquire(" ", 1, TimeSpan.FromSeconds(1))).Throws<ArgumentException>();
        await Assert.That(() => limiter.TryAcquire("a", 0, TimeSpan.FromSeconds(1))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => limiter.TryAcquire("a", 1, TimeSpan.Zero)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>JSONL audit output is append-only, redacted by shape, and disposable.</summary>
    /// <returns>A task that verifies audit persistence.</returns>
    [Test]
    public async Task Json_line_audit_sink_writes_one_event_per_line()
    {
        var root = Path.Combine(Path.GetTempPath(), "uiinspect-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "audit", "actions.jsonl");
        var auditEvent = new AuditEvent(
            DateTimeOffset.Parse(InitialUtcText),
            "event",
            "action",
            "completed",
            "hash",
            TestProcessId,
            DateTimeOffset.Parse("2026-07-27T11:00:00Z"),
            "invoke",
            null);
        var sink = new JsonLineAuditSink(path);

        await sink.WriteAsync(auditEvent, CancellationToken.None);
        await sink.WriteAsync(auditEvent with { EventId = "event-2" }, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(path);
        using var json = JsonDocument.Parse(lines[0]);

        await Assert.That(lines.Length).IsEqualTo(ExpectedAuditLineCount);
        await Assert.That(json.RootElement.GetProperty("eventId").GetString()).IsEqualTo("event");
        sink.Dispose();
        sink.Dispose();
        await Assert.That(() => sink.WriteAsync(auditEvent, CancellationToken.None).AsTask()).Throws<ObjectDisposedException>();
        await Assert.That(static () => new JsonLineAuditSink(" ")).Throws<ArgumentException>();

        Directory.Delete(root, true);
    }

    /// <summary>Semantic selector display uses ID, name, and class fallbacks safely.</summary>
    /// <returns>A task that verifies selector display.</returns>
    [Test]
    public async Task Element_selector_display_is_explanatory()
    {
        var byId = new ElementSelector("Button", "a\"b", "name", "class", 0);
        var byName = new ElementSelector("Text", string.Empty, "a\\b", "class", 1);
        var byClass = new ElementSelector("Pane", string.Empty, string.Empty, "custom", ThirdOrdinal);
        var locator = new ElementLocator([byId, byName, byClass]);

        await Assert.That(byId.Display).IsEqualTo("Button[id=\"a\\\"b\",#0]");
        await Assert.That(byName.Display).IsEqualTo("Text[name=\"a\\\\b\",#1]");
        await Assert.That(byClass.Display).IsEqualTo("Pane[class=\"custom\",#2]");
        await Assert.That(locator.Segments.Count).IsEqualTo(ExpectedSegmentCount);
    }

    /// <summary>The Windows process resolver handles current, invalid, and cancelled requests.</summary>
    /// <returns>A task that verifies process identity resolution.</returns>
    [Test]
    public async Task Windows_process_identity_provider_resolves_live_process()
    {
        var provider = new WindowsProcessIdentityProvider();
        var current = await provider.ResolveAsync(Environment.ProcessId, CancellationToken.None);
        var invalid = await provider.ResolveAsync(-1, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(current).IsNotNull();
        await Assert.That(current!.ProcessId).IsEqualTo(Environment.ProcessId);
        await Assert.That(invalid).IsNull();
        await Assert.That(() => provider.ResolveAsync(Environment.ProcessId, cancellation.Token).AsTask()).Throws<OperationCanceledException>();
    }
}

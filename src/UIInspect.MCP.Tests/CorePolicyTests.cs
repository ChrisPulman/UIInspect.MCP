// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Text.Json;
using UIInspect.MCP.Core.Auditing;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Windows.Automation;
using UIInspect.MCP.Windows.Processes;

namespace UIInspect.MCP.Tests;

/// <summary>Tests pure models, consent, rate limiting, audit persistence, and process identity.</summary>
public sealed class CorePolicyTests
{
    /// <summary>Result factories preserve success and safe error data.</summary>
    [Test]
    public async Task Result_factories_are_consistent()
    {
        var ok = UiResult<string>.Ok("value");
        var failed = UiResult<string>.Fail("rate_limited", "wait", TimeSpan.FromMilliseconds(25));
        var platformOk = PlatformActionResult.Ok("done");
        var platformFailure = PlatformActionResult.Fail("failed", "no");

        await Assert.That(ok.Success).IsTrue();
        await Assert.That(ok.Data).IsEqualTo("value");
        await Assert.That(ok.Error).IsNull();
        await Assert.That(failed.Success).IsFalse();
        await Assert.That(failed.Data).IsNull();
        await Assert.That(failed.Error!.RetryAfterMilliseconds).IsEqualTo(25);
        await Assert.That(platformOk.Succeeded).IsTrue();
        await Assert.That(platformFailure.ErrorCode).IsEqualTo("failed");
    }

    /// <summary>Audit hashing is deterministic and rejects blank identities.</summary>
    [Test]
    public async Task Audit_hash_is_safe_and_deterministic()
    {
        var first = AuditHash.Compute("client");
        var second = AuditHash.Compute("client");

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first.Length).IsEqualTo(64);
        await Assert.That(first).DoesNotContain("client");
        await Assert.That(() => AuditHash.Compute(" ")).Throws<ArgumentException>();
    }

    /// <summary>Consent is scoped, expiring, revocable, and bound to an exact target.</summary>
    [Test]
    public async Task Consent_registry_enforces_scope_expiry_and_revocation()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-27T12:00:00Z"));
        var registry = new ConsentRegistry(time);
        var target = new ProcessIdentity(10, time.UtcNow, "app", "app.exe", 1);
        var other = target with { StartedAtUtc = target.StartedAtUtc.AddSeconds(1) };
        var first = registry.Grant("client", target, UiCapability.Inspect, TimeSpan.FromMinutes(1));
        var second = registry.Grant("client", other, UiCapability.Interact, TimeSpan.FromMinutes(2));

        await Assert.That(registry.FindActive("client", target, UiCapability.Inspect)).IsEqualTo(first);
        await Assert.That(registry.FindActive("other-client", target, UiCapability.Inspect)).IsNull();
        await Assert.That(registry.FindActive("client", target, UiCapability.Interact)).IsNull();
        await Assert.That(registry.Revoke(Guid.NewGuid())).IsFalse();
        await Assert.That(registry.Revoke(first.Id)).IsTrue();
        await Assert.That(registry.FindActive("client", target, UiCapability.Inspect)).IsNull();
        await Assert.That(registry.RevokeTarget(target)).IsEqualTo(0);
        await Assert.That(registry.RevokeTarget(other)).IsEqualTo(1);

        var expiring = registry.Grant("client", target, UiCapability.Inspect, TimeSpan.FromSeconds(1));
        time.Advance(TimeSpan.FromSeconds(1));
        await Assert.That(registry.FindActive("client", target, UiCapability.Inspect)).IsNull();
        await Assert.That(registry.Revoke(expiring.Id)).IsFalse();

        await Assert.That(() => new ConsentRegistry(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => registry.Grant(" ", target, UiCapability.Inspect, TimeSpan.FromSeconds(1))).Throws<ArgumentException>();
        await Assert.That(() => registry.Grant("client", null!, UiCapability.Inspect, TimeSpan.FromSeconds(1))).Throws<ArgumentNullException>();
        await Assert.That(() => registry.Grant("client", target, UiCapability.Inspect, TimeSpan.Zero)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>The fixed-window limiter resets on time and separates buckets.</summary>
    [Test]
    public async Task Fixed_window_rate_limiter_is_deterministic()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-27T12:00:00Z"));
        var limiter = new FixedWindowRateLimiter(time);

        await Assert.That(limiter.TryAcquire("a", 2, TimeSpan.FromSeconds(10)).IsAllowed).IsTrue();
        await Assert.That(limiter.TryAcquire("a", 2, TimeSpan.FromSeconds(10)).IsAllowed).IsTrue();
        var blocked = limiter.TryAcquire("a", 2, TimeSpan.FromSeconds(10));
        await Assert.That(blocked.IsAllowed).IsFalse();
        await Assert.That(blocked.RetryAfter).IsEqualTo(TimeSpan.FromSeconds(10));
        await Assert.That(limiter.TryAcquire("b", 1, TimeSpan.FromSeconds(10)).IsAllowed).IsTrue();
        time.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(limiter.TryAcquire("a", 2, TimeSpan.FromSeconds(10)).IsAllowed).IsTrue();

        await Assert.That(() => new FixedWindowRateLimiter(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => limiter.TryAcquire(" ", 1, TimeSpan.FromSeconds(1))).Throws<ArgumentException>();
        await Assert.That(() => limiter.TryAcquire("a", 0, TimeSpan.FromSeconds(1))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => limiter.TryAcquire("a", 1, TimeSpan.Zero)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>JSONL audit output is append-only, redacted by shape, and disposable.</summary>
    [Test]
    public async Task Json_line_audit_sink_writes_one_event_per_line()
    {
        var root = Path.Combine(Path.GetTempPath(), "uiinspect-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "audit", "actions.jsonl");
        var auditEvent = new AuditEvent(
            DateTimeOffset.Parse("2026-07-27T12:00:00Z"),
            "event",
            "action",
            "completed",
            "hash",
            10,
            DateTimeOffset.Parse("2026-07-27T11:00:00Z"),
            "invoke",
            null);
        using var sink = new JsonLineAuditSink(path);

        await sink.WriteAsync(auditEvent, CancellationToken.None);
        await sink.WriteAsync(auditEvent with { EventId = "event-2" }, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(path);
        using var json = JsonDocument.Parse(lines[0]);

        await Assert.That(lines.Length).IsEqualTo(2);
        await Assert.That(json.RootElement.GetProperty("eventId").GetString()).IsEqualTo("event");
        sink.Dispose();
        await Assert.That(() => sink.WriteAsync(auditEvent, CancellationToken.None).AsTask()).Throws<ObjectDisposedException>();
        await Assert.That(() => new JsonLineAuditSink(" ")).Throws<ArgumentException>();

        Directory.Delete(root, true);
    }

    /// <summary>Semantic selector display uses ID, name, and class fallbacks safely.</summary>
    [Test]
    public async Task Element_selector_display_is_explanatory()
    {
        var byId = new ElementSelector("Button", "a\"b", "name", "class", 0);
        var byName = new ElementSelector("Text", string.Empty, "a\\b", "class", 1);
        var byClass = new ElementSelector("Pane", string.Empty, string.Empty, "custom", 2);
        var locator = new ElementLocator([byId, byName, byClass]);

        await Assert.That(byId.Display).IsEqualTo("Button[id=\"a\\\"b\"]");
        await Assert.That(byName.Display).IsEqualTo("Text[name=\"a\\\\b\",#1]");
        await Assert.That(byClass.Display).IsEqualTo("Pane[class=\"custom\",#2]");
        await Assert.That(locator.Segments.Count).IsEqualTo(3);
    }

    /// <summary>The Windows process resolver handles current, invalid, and cancelled requests.</summary>
    [Test]
    public async Task Windows_process_identity_provider_resolves_live_process()
    {
        var provider = new WindowsProcessIdentityProvider();
        var current = await provider.ResolveAsync(Environment.ProcessId, CancellationToken.None);
        var invalid = await provider.ResolveAsync(-1, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(current).IsNotNull();
        await Assert.That(current!.ProcessId).IsEqualTo(Environment.ProcessId);
        await Assert.That(invalid).IsNull();
        await Assert.That(() => provider.ResolveAsync(Environment.ProcessId, cancellation.Token).AsTask()).Throws<OperationCanceledException>();
    }
}

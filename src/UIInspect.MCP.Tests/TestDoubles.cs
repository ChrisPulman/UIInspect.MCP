// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Core.Services;

namespace UIInspect.MCP.Tests;

internal sealed class FakeTimeProvider : TimeProvider
{
    public FakeTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;

    public DateTimeOffset UtcNow { get; private set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}

internal sealed class FakeProcessIdentityProvider : IProcessIdentityProvider
{
    public ProcessIdentity? Current { get; set; }

    public Queue<ProcessIdentity?> Responses { get; } = new();

    public ValueTask<ProcessIdentity?> ResolveAsync(int processId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Responses.TryDequeue(out var response) ? response : Current);
    }
}

internal sealed class FakeConsentPrompt : IUserConsentPrompt
{
    public bool Approved { get; set; } = true;

    public List<(ProcessIdentity Target, UiCapability Capabilities, string ClientId)> Requests { get; } = [];

    public ValueTask<bool> RequestAsync(
        ProcessIdentity target,
        UiCapability capabilities,
        string clientId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add((target, capabilities, clientId));
        return ValueTask.FromResult(Approved);
    }
}

internal sealed class FakeRateLimiter : IOperationRateLimiter
{
    public Queue<RateLimitDecision> Decisions { get; } = new();

    public List<string> Buckets { get; } = [];

    public RateLimitDecision TryAcquire(string bucket, int permitLimit, TimeSpan window)
    {
        Buckets.Add(bucket);
        return Decisions.TryDequeue(out var decision)
            ? decision
            : new RateLimitDecision(true, TimeSpan.Zero);
    }
}

internal sealed class FakeAuditSink : IAuditSink
{
    public List<AuditEvent> Events { get; } = [];

    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Events.Add(auditEvent);
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeUiAutomationBackend : IUiAutomationBackend
{
    public FakeUiAutomationBackend(FakeUiAutomationSession session) => Session = session;

    public FakeUiAutomationSession Session { get; set; }

    public IReadOnlyList<WindowDescriptor> Windows { get; set; } = [];

    public int AttachCalls { get; private set; }

    public ValueTask<IReadOnlyList<WindowDescriptor>> ListTopLevelWindowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Windows);
    }

    public ValueTask<IUiAutomationSession> AttachAsync(
        ProcessIdentity target,
        long? windowHandle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttachCalls++;
        return ValueTask.FromResult<IUiAutomationSession>(Session);
    }
}

internal sealed class FakeUiAutomationSession : IUiAutomationSession
{
    public FakeUiAutomationSession(ProcessIdentity target, long windowHandle = 42)
    {
        Target = target;
        WindowHandle = windowHandle;
        Snapshot = new UiTreeSnapshot(
            "pending",
            1,
            [
                new UiElementNode(
                    "e_1_0",
                    null,
                    "$window",
                    0,
                    "Window",
                    "Fixture",
                    "Root",
                    "FixtureClass",
                    "WPF",
                    true,
                    false,
                    false,
                    new UiRectangle(1, 2, 3, 4),
                    ["Window"]),
            ],
            false);
    }

    public ProcessIdentity Target { get; }

    public long WindowHandle { get; }

    public UiTreeSnapshot Snapshot { get; set; }

    public Queue<PlatformActionResult> Results { get; } = new();

    public List<string> Calls { get; } = [];

    public int DisposeCalls { get; private set; }

    public ValueTask<UiTreeSnapshot> InspectAsync(
        string sessionId,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add($"inspect:{maxDepth}:{maxNodes}");
        return ValueTask.FromResult(Snapshot with { SessionId = sessionId });
    }

    public ValueTask<PlatformActionResult> InvokeAsync(string elementReference, CancellationToken cancellationToken) =>
        ActionAsync($"invoke:{elementReference}", cancellationToken);

    public ValueTask<PlatformActionResult> ClickAsync(string elementReference, CancellationToken cancellationToken) =>
        ActionAsync($"click:{elementReference}", cancellationToken);

    public ValueTask<PlatformActionResult> SetValueAsync(
        string elementReference,
        string value,
        CancellationToken cancellationToken) =>
        ActionAsync($"value:{elementReference}:{value}", cancellationToken);

    public ValueTask<PlatformActionResult> SelectAsync(string elementReference, CancellationToken cancellationToken) =>
        ActionAsync($"select:{elementReference}", cancellationToken);

    public ValueTask<PlatformActionResult> SetExpandedAsync(
        string elementReference,
        bool expand,
        CancellationToken cancellationToken) =>
        ActionAsync($"{(expand ? "expand" : "collapse")}:{elementReference}", cancellationToken);

    public ValueTask<PlatformActionResult> SendKeyAsync(
        string elementReference,
        string key,
        CancellationToken cancellationToken) =>
        ActionAsync($"key:{elementReference}:{key}", cancellationToken);

    public ValueTask DisposeAsync()
    {
        DisposeCalls++;
        return ValueTask.CompletedTask;
    }

    private ValueTask<PlatformActionResult> ActionAsync(
        string call,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(call);
        return ValueTask.FromResult(
            Results.TryDequeue(out var result)
                ? result
                : PlatformActionResult.Ok("completed"));
    }
}

internal sealed class ServiceHarness : IAsyncDisposable
{
    public const string ClientId = "test-client";

    public ServiceHarness(UiInspectOptions? options = null)
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero));
        Target = new ProcessIdentity(1234, Time.UtcNow.AddHours(-1), "Fixture", @"C:\Fixture.exe", 1);
        Processes = new FakeProcessIdentityProvider { Current = Target };
        Prompt = new FakeConsentPrompt();
        RateLimiter = new FakeRateLimiter();
        Audit = new FakeAuditSink();
        Session = new FakeUiAutomationSession(Target);
        Backend = new FakeUiAutomationBackend(Session)
        {
            Windows =
            [
                new WindowDescriptor(Target, 42, "Fixture", "FixtureClass", "WPF", true, false),
            ],
        };
        Options = options ?? new UiInspectOptions
        {
            DiscoveryRatePerMinute = 100,
            InspectionRatePerMinute = 100,
            ActionRatePerMinute = 100,
            ConsentPromptRatePerMinute = 100,
        };
        Consent = new ConsentRegistry(Time);
        Service = new UiInspectService(
            Backend,
            Processes,
            Prompt,
            Consent,
            RateLimiter,
            Audit,
            Time,
            Options);
    }

    public FakeTimeProvider Time { get; }

    public ProcessIdentity Target { get; }

    public FakeProcessIdentityProvider Processes { get; }

    public FakeConsentPrompt Prompt { get; }

    public FakeRateLimiter RateLimiter { get; }

    public FakeAuditSink Audit { get; }

    public FakeUiAutomationSession Session { get; }

    public FakeUiAutomationBackend Backend { get; }

    public UiInspectOptions Options { get; }

    public ConsentRegistry Consent { get; }

    public UiInspectService Service { get; }

    public async ValueTask<string> AttachWithConsentAsync(
        bool allowActions = true,
        bool allowKeyboard = true)
    {
        var consent = await Service.RequestConsentAsync(
            Target.ProcessId,
            allowActions,
            allowKeyboard,
            ClientId);
        var attach = await Service.AttachAsync(Target.ProcessId, null, ClientId);
        return attach.Data!.SessionId;
    }

    public ValueTask DisposeAsync() => Service.DisposeAsync();
}

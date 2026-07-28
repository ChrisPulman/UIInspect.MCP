// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Core.Services;

namespace UIInspect.MCP.Tests;

/// <summary>Composes deterministic dependencies for service-level tests.</summary>
internal sealed class ServiceHarness : IAsyncDisposable
{
    /// <summary>Stable client identity used by test calls.</summary>
    internal const string ClientId = "test-client";

    /// <summary>Fixture process identifier.</summary>
    private const int FixtureProcessId = 1234;

    /// <summary>Fixture native window handle.</summary>
    private const long FixtureWindowHandle = 42;

    /// <summary>Rate limit that does not constrain ordinary test execution.</summary>
    private const int GenerousRateLimit = 100;

    /// <summary>Initializes a new instance of the <see cref="ServiceHarness"/> class.</summary>
    /// <param name="options">Optional safety options.</param>
    public ServiceHarness(UiInspectOptions? options = null)
    {
        Time = new(new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero));
        Target = new(FixtureProcessId, Time.UtcNow.AddHours(-1), "Fixture", @"C:\Fixture.exe", 1);
        Processes = new() { Current = Target };
        Prompt = new();
        RateLimiter = new();
        Audit = new();
        Session = new(Target);
        Backend = new(Session) { Windows = [new WindowDescriptor(Target, FixtureWindowHandle, "Fixture", "FixtureClass", "WPF", true, false)] };
        Options = options ?? CreateDefaultOptions();
        Consent = new(Time);
        Service = new(
            new UiInspectServiceDependencies(Backend, Processes, Prompt, Consent, RateLimiter, Audit, Time),
            Options);
    }

    /// <summary>Gets the controllable clock.</summary>
    internal FakeTimeProvider Time { get; }

    /// <summary>Gets the target process identity.</summary>
    internal ProcessIdentity Target { get; }

    /// <summary>Gets the fake process resolver.</summary>
    internal FakeProcessIdentityProvider Processes { get; }

    /// <summary>Gets the fake consent prompt.</summary>
    internal FakeConsentPrompt Prompt { get; }

    /// <summary>Gets the fake rate limiter.</summary>
    internal FakeRateLimiter RateLimiter { get; }

    /// <summary>Gets the fake audit sink.</summary>
    internal FakeAuditSink Audit { get; }

    /// <summary>Gets the fake UI Automation session.</summary>
    internal FakeUiAutomationSession Session { get; }

    /// <summary>Gets the fake UI Automation backend.</summary>
    internal FakeUiAutomationBackend Backend { get; }

    /// <summary>Gets service safety options.</summary>
    internal UiInspectOptions Options { get; }

    /// <summary>Gets the consent registry.</summary>
    internal ConsentRegistry Consent { get; }

    /// <summary>Gets the service under test.</summary>
    internal UiInspectService Service { get; }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => Service.DisposeAsync();

    /// <summary>Obtains action-capable consent and attaches a session.</summary>
    /// <param name="allowActions">Whether interaction capability is requested.</param>
    /// <param name="allowKeyboard">Whether keyboard capability is requested.</param>
    /// <returns>Opaque attached session identifier.</returns>
    internal async ValueTask<string> AttachWithConsentAsync(bool allowActions = true, bool allowKeyboard = true)
    {
        _ = await Service.RequestConsentAsync(Target.ProcessId, allowActions, allowKeyboard, ClientId, CancellationToken.None);
        var attach = await Service.AttachAsync(Target.ProcessId, null, ClientId, CancellationToken.None);
        return attach.Data!.SessionId;
    }

    /// <summary>Creates non-restrictive defaults for ordinary harness tests.</summary>
    /// <returns>Configured safety options.</returns>
    private static UiInspectOptions CreateDefaultOptions() => new()
    {
        DiscoveryRatePerMinute = GenerousRateLimit,
        InspectionRatePerMinute = GenerousRateLimit,
        ActionRatePerMinute = GenerousRateLimit,
        ConsentPromptRatePerMinute = GenerousRateLimit,
    };
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>In-memory UI Automation session that records semantic operations.</summary>
internal sealed class FakeUiAutomationSession : IUiAutomationSession
{
    /// <summary>Fixture rectangle height.</summary>
    private const double RectangleHeight = 4;

    /// <summary>Fixture rectangle width.</summary>
    private const double RectangleWidth = 3;

    /// <summary>Fixture rectangle horizontal origin.</summary>
    private const double RectangleX = 1;

    /// <summary>Fixture rectangle vertical origin.</summary>
    private const double RectangleY = 2;

    /// <summary>Number of disposal operations.</summary>
    private int _disposeCalls;

    /// <summary>Initializes a new instance of the <see cref="FakeUiAutomationSession"/> class.</summary>
    /// <param name="target">Process represented by this session.</param>
    /// <param name="windowHandle">Native window handle represented by this session.</param>
    public FakeUiAutomationSession(ProcessIdentity target, long windowHandle = 42)
    {
        Target = target;
        WindowHandle = windowHandle;
        Snapshot = new(
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
                    new UiRectangle(RectangleX, RectangleY, RectangleWidth, RectangleHeight),
                    ["Window"]),
            ],
            false);
    }

    /// <summary>Gets the process identity represented by the session.</summary>
    public ProcessIdentity Target { get; }

    /// <summary>Gets the attached native window handle.</summary>
    public long WindowHandle { get; }

    /// <summary>Gets or sets the UI snapshot returned during inspection.</summary>
    internal UiTreeSnapshot Snapshot { get; set; }

    /// <summary>Gets queued operation results.</summary>
    internal Queue<PlatformActionResult> Results { get; } = new();

    /// <summary>Gets recorded semantic operation calls.</summary>
    internal List<string> Calls { get; } = [];

    /// <summary>Gets the number of disposal operations.</summary>
    internal int DisposeCalls => Volatile.Read(ref _disposeCalls);

    /// <inheritdoc/>
    public ValueTask<UiTreeSnapshot> InspectAsync(string sessionId, int maxDepth, int maxNodes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add($"inspect:{maxDepth}:{maxNodes}");
        return ValueTask.FromResult(Snapshot with { SessionId = sessionId });
    }

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> InvokeAsync(string elementReference, CancellationToken cancellationToken) =>
        ActionAsync($"invoke:{elementReference}", cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> ClickAsync(string elementReference, CancellationToken cancellationToken) =>
        ActionAsync($"click:{elementReference}", cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SetValueAsync(string elementReference, string value, CancellationToken cancellationToken) =>
        ActionAsync($"value:{elementReference}:{value}", cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SelectAsync(string elementReference, CancellationToken cancellationToken) =>
        ActionAsync($"select:{elementReference}", cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SetExpandedAsync(string elementReference, bool expand, CancellationToken cancellationToken) =>
        ActionAsync($"{(expand ? "expand" : "collapse")}:{elementReference}", cancellationToken);

    /// <inheritdoc/>
    public ValueTask<PlatformActionResult> SendKeyAsync(string elementReference, string key, CancellationToken cancellationToken) =>
        ActionAsync($"key:{elementReference}:{key}", cancellationToken);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _disposeCalls);
        return ValueTask.CompletedTask;
    }

    /// <summary>Records and resolves one action.</summary>
    /// <param name="call">Recorded action name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Queued action result or a default success result.</returns>
    private ValueTask<PlatformActionResult> ActionAsync(string call, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(call);
        return ValueTask.FromResult(Results.TryDequeue(out var result) ? result : PlatformActionResult.Ok("completed"));
    }
}

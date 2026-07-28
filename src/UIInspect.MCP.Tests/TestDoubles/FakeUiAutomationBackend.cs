// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>Captures UI Automation backend interactions.</summary>
internal sealed class FakeUiAutomationBackend : IUiAutomationBackend
{
    /// <summary>Initializes a new instance of the <see cref="FakeUiAutomationBackend"/> class.</summary>
    /// <param name="session">Session returned by successful attachment.</param>
    public FakeUiAutomationBackend(FakeUiAutomationSession session) => Session = session;

    /// <summary>Gets or sets the session returned by attachment.</summary>
    internal FakeUiAutomationSession Session { get; set; }

    /// <summary>Gets or sets discoverable desktop windows.</summary>
    internal IReadOnlyList<WindowDescriptor> Windows { get; set; } = [];

    /// <summary>Gets the number of attachment requests.</summary>
    internal int AttachCalls { get; private set; }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WindowDescriptor>> ListTopLevelWindowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Windows);
    }

    /// <inheritdoc/>
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

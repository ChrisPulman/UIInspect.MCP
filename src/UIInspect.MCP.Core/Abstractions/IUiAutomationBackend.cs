// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>Creates UI Automation discovery results and attached sessions.</summary>
public interface IUiAutomationBackend
{
    /// <summary>List visible top-level windows on the current interactive desktop.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Top-level windows.</returns>
    ValueTask<IReadOnlyList<WindowDescriptor>> ListTopLevelWindowsAsync(CancellationToken cancellationToken);

    /// <summary>Attach to an exact process instance and optional top-level window.</summary>
    /// <param name="target">Target process instance.</param>
    /// <param name="windowHandle">Optional native window handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attached platform session.</returns>
    ValueTask<IUiAutomationSession> AttachAsync(ProcessIdentity target, long? windowHandle, CancellationToken cancellationToken);
}

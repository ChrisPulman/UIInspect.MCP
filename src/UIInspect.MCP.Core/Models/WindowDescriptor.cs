// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>A top-level window that may be attached.</summary>
/// <param name="Process">Owning process instance.</param>
/// <param name="WindowHandle">Native top-level window handle.</param>
/// <param name="Title">UI Automation window name.</param>
/// <param name="ClassName">Native or framework window class.</param>
/// <param name="FrameworkId">UI Automation framework identifier.</param>
/// <param name="IsEnabled">Whether the window is enabled.</param>
/// <param name="IsOffscreen">Whether the provider reports it as offscreen.</param>
public sealed record WindowDescriptor(ProcessIdentity Process, long WindowHandle, string Title, string ClassName, string FrameworkId, bool IsEnabled, bool IsOffscreen);

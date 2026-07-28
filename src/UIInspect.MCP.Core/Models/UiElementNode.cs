// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>A flattened semantic UI Automation tree node.</summary>
/// <param name="ElementReference">Opaque, session-scoped element reference.</param>
/// <param name="ParentReference">Opaque parent reference, or null for the root.</param>
/// <param name="StablePath">Human-readable semantic path used to explain element identity.</param>
/// <param name="Depth">Depth relative to the attached root.</param>
/// <param name="ControlType">UI Automation control type.</param>
/// <param name="Name">Accessible element name; password elements are redacted.</param>
/// <param name="AutomationId">Provider automation identifier.</param>
/// <param name="ClassName">Provider class name.</param>
/// <param name="FrameworkId">Provider framework identifier.</param>
/// <param name="IsEnabled">Whether actions are enabled.</param>
/// <param name="IsOffscreen">Whether the provider reports the element as offscreen.</param>
/// <param name="IsPassword">Whether the provider marks the element as password content.</param>
/// <param name="Bounds">Bounding rectangle reported by UI Automation.</param>
/// <param name="SupportedPatterns">Supported semantic action patterns.</param>
public sealed record UiElementNode(
    string ElementReference,
    string? ParentReference,
    string StablePath,
    int Depth,
    string ControlType,
    string Name,
    string AutomationId,
    string ClassName,
    string FrameworkId,
    bool IsEnabled,
    bool IsOffscreen,
    bool IsPassword,
    UiRectangle Bounds,
    IReadOnlyList<string> SupportedPatterns);

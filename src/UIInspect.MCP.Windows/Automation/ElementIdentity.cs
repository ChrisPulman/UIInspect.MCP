// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Windows.Automation;

/// <summary>Provider-neutral identity values used to compare semantic siblings.</summary>
/// <param name="ControlType">UI Automation control type.</param>
/// <param name="AutomationId">Automation identifier, when provided by the target.</param>
/// <param name="Name">Accessible element name.</param>
/// <param name="ClassName">Provider class name.</param>
internal sealed record ElementIdentity(
    string ControlType,
    string AutomationId,
    string Name,
    string ClassName);

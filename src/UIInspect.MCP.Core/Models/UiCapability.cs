// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Capabilities granted by explicit user consent.</summary>
[Flags]
public enum UiCapability
{
    /// <summary>No capability.</summary>
    None = 0,

    /// <summary>Attach to a target and inspect its semantic UI Automation tree.</summary>
    Inspect = 1,

    /// <summary>Invoke or change semantic controls.</summary>
    Interact = 2,

    /// <summary>Send a logical keyboard key to a resolved element.</summary>
    Keyboard = 4,
}

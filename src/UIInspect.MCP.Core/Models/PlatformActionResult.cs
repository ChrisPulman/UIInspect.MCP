// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Platform adapter action outcome.</summary>
/// <param name="Succeeded">Whether the provider completed the action.</param>
/// <param name="ErrorCode">Stable provider error code.</param>
/// <param name="Message">Safe action result.</param>
public sealed record PlatformActionResult(bool Succeeded, string? ErrorCode, string Message)
{
    /// <summary>Create a successful provider result.</summary>
    /// <param name="message">Safe message.</param>
    /// <returns>Successful provider result.</returns>
    public static PlatformActionResult Ok(string message) => new(true, null, message);

    /// <summary>Create a failed provider result.</summary>
    /// <param name="code">Stable code.</param>
    /// <param name="message">Safe message.</param>
    /// <returns>Failed provider result.</returns>
    public static PlatformActionResult Fail(string code, string message) => new(false, code, message);
}

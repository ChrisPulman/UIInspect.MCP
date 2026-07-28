// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>Shows an out-of-band, trusted consent prompt to the local user.</summary>
public interface IUserConsentPrompt
{
    /// <summary>Request consent for an exact process instance and capability set.</summary>
    /// <param name="target">Exact process instance.</param>
    /// <param name="capabilities">Requested capabilities.</param>
    /// <param name="clientId">Initiating MCP client identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True only when the user explicitly approves.</returns>
    ValueTask<bool> RequestAsync(ProcessIdentity target, UiCapability capabilities, string clientId, CancellationToken cancellationToken);
}

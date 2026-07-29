// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>Coordinates trusted consent decisions for one server session.</summary>
public interface ISessionUserConsentPrompt
{
    /// <summary>Request or replay consent for an exact process instance and capability set.</summary>
    /// <param name="target">Exact target process instance.</param>
    /// <param name="capabilities">Requested capabilities.</param>
    /// <param name="clientId">Initiating MCP client identity.</param>
    /// <param name="cancellationToken">Cancellation token for this caller's wait.</param>
    /// <returns>The shared server-session decision.</returns>
    ValueTask<SessionConsentDecision> RequestAsync(
        ProcessIdentity target,
        UiCapability capabilities,
        string clientId,
        CancellationToken cancellationToken);
}

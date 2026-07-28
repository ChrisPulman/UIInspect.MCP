// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>Resolves live process instances independently from UI Automation.</summary>
public interface IProcessIdentityProvider
{
    /// <summary>Resolve a process instance, or null when it is unavailable or inaccessible.</summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved process instance.</returns>
    ValueTask<ProcessIdentity?> ResolveAsync(int processId, CancellationToken cancellationToken);
}

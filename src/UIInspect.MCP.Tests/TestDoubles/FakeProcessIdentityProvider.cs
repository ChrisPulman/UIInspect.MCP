// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>Queue-driven process identity resolver for tests.</summary>
internal sealed class FakeProcessIdentityProvider : IProcessIdentityProvider
{
    /// <summary>Gets or sets the default identity returned after queued responses.</summary>
    internal ProcessIdentity? Current { get; set; }

    /// <summary>Gets queued responses for successive resolution attempts.</summary>
    internal Queue<ProcessIdentity?> Responses { get; } = new();

    /// <inheritdoc/>
    public ValueTask<ProcessIdentity?> ResolveAsync(int processId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Responses.TryDequeue(out var response) ? response : Current);
    }
}

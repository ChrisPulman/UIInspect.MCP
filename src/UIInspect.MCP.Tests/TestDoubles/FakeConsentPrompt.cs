// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>Captures and controls local-user consent decisions.</summary>
internal sealed class FakeConsentPrompt : IUserConsentPrompt
{
    /// <summary>Gets or sets whether the next prompt is approved.</summary>
    internal bool Approved { get; set; } = true;

    /// <summary>Gets observed consent requests.</summary>
    internal ConcurrentQueue<(ProcessIdentity Target, UiCapability Capabilities, string ClientId)> Requests { get; } = new();

    /// <summary>Gets or sets an optional externally controlled asynchronous decision.</summary>
    internal TaskCompletionSource<bool>? DecisionSource { get; set; }

    /// <inheritdoc/>
    public ValueTask<bool> RequestAsync(
        ProcessIdentity target,
        UiCapability capabilities,
        string clientId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Enqueue((target, capabilities, clientId));
        return DecisionSource is null
            ? ValueTask.FromResult(Approved)
            : new ValueTask<bool>(DecisionSource.Task.WaitAsync(cancellationToken));
    }
}

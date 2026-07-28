// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>One serialized UI Automation connection.</summary>
public interface IUiAutomationSession : IAsyncDisposable
{
    /// <summary>Gets the exact target bound to this session.</summary>
    ProcessIdentity Target { get; }

    /// <summary>Gets the top-level native window handle.</summary>
    long WindowHandle { get; }

    /// <summary>Create a bounded semantic snapshot and a fresh reference generation.</summary>
    /// <param name="sessionId">Owning public session identifier.</param>
    /// <param name="maxDepth">Maximum descendant depth.</param>
    /// <param name="maxNodes">Maximum flattened node count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tree snapshot.</returns>
    ValueTask<UiTreeSnapshot> InspectAsync(string sessionId, int maxDepth, int maxNodes, CancellationToken cancellationToken);

    /// <summary>Invoke an element through InvokePattern.</summary>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider action result.</returns>
    ValueTask<PlatformActionResult> InvokeAsync(string elementReference, CancellationToken cancellationToken);

    /// <summary>Click the center of a semantically resolved element.</summary>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider action result.</returns>
    ValueTask<PlatformActionResult> ClickAsync(string elementReference, CancellationToken cancellationToken);

    /// <summary>Set an element through ValuePattern.</summary>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="value">Value to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider action result.</returns>
    ValueTask<PlatformActionResult> SetValueAsync(string elementReference, string value, CancellationToken cancellationToken);

    /// <summary>Select an element through SelectionItemPattern.</summary>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider action result.</returns>
    ValueTask<PlatformActionResult> SelectAsync(string elementReference, CancellationToken cancellationToken);

    /// <summary>Expand or collapse an element through ExpandCollapsePattern.</summary>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="expand">Whether to expand the element.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider action result.</returns>
    ValueTask<PlatformActionResult> SetExpandedAsync(string elementReference, bool expand, CancellationToken cancellationToken);

    /// <summary>Focus an element and send one allowlisted logical key.</summary>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="key">Logical key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider action result.</returns>
    ValueTask<PlatformActionResult> SendKeyAsync(string elementReference, string key, CancellationToken cancellationToken);
}

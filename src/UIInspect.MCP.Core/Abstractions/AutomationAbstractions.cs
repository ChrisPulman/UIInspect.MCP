// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
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

/// <summary>Shows an out-of-band, trusted consent prompt to the local user.</summary>
public interface IUserConsentPrompt
{
    /// <summary>Request consent for an exact process instance and capability set.</summary>
    /// <param name="target">Exact process instance.</param>
    /// <param name="capabilities">Requested capabilities.</param>
    /// <param name="clientId">Initiating MCP client identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True only when the user explicitly approves.</returns>
    ValueTask<bool> RequestAsync(
        ProcessIdentity target,
        UiCapability capabilities,
        string clientId,
        CancellationToken cancellationToken);
}

/// <summary>Creates UI Automation discovery results and attached sessions.</summary>
public interface IUiAutomationBackend
{
    /// <summary>List visible top-level windows on the current interactive desktop.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Top-level windows.</returns>
    ValueTask<IReadOnlyList<WindowDescriptor>> ListTopLevelWindowsAsync(CancellationToken cancellationToken);

    /// <summary>Attach to an exact process instance and optional top-level window.</summary>
    /// <param name="target">Target process instance.</param>
    /// <param name="windowHandle">Optional native window handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attached platform session.</returns>
    ValueTask<IUiAutomationSession> AttachAsync(
        ProcessIdentity target,
        long? windowHandle,
        CancellationToken cancellationToken);
}

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
    ValueTask<UiTreeSnapshot> InspectAsync(
        string sessionId,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken);

    /// <summary>Invoke an element through InvokePattern.</summary>
    ValueTask<PlatformActionResult> InvokeAsync(string elementReference, CancellationToken cancellationToken);

    /// <summary>Click the center of a semantically resolved element.</summary>
    ValueTask<PlatformActionResult> ClickAsync(string elementReference, CancellationToken cancellationToken);

    /// <summary>Set an element through ValuePattern.</summary>
    ValueTask<PlatformActionResult> SetValueAsync(
        string elementReference,
        string value,
        CancellationToken cancellationToken);

    /// <summary>Select an element through SelectionItemPattern.</summary>
    ValueTask<PlatformActionResult> SelectAsync(string elementReference, CancellationToken cancellationToken);

    /// <summary>Expand or collapse an element through ExpandCollapsePattern.</summary>
    ValueTask<PlatformActionResult> SetExpandedAsync(
        string elementReference,
        bool expand,
        CancellationToken cancellationToken);

    /// <summary>Focus an element and send one allowlisted logical key.</summary>
    ValueTask<PlatformActionResult> SendKeyAsync(
        string elementReference,
        string key,
        CancellationToken cancellationToken);
}

/// <summary>Writes redacted security audit events.</summary>
public interface IAuditSink
{
    /// <summary>Write one redacted event.</summary>
    /// <param name="auditEvent">Event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completion.</returns>
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>Applies a bounded operation rate policy.</summary>
public interface IOperationRateLimiter
{
    /// <summary>Try to acquire one operation permit.</summary>
    /// <param name="bucket">Non-secret bucket identity.</param>
    /// <param name="permitLimit">Maximum permits per window.</param>
    /// <param name="window">Rate window.</param>
    /// <returns>Rate decision.</returns>
    RateLimitDecision TryAcquire(string bucket, int permitLimit, TimeSpan window);
}

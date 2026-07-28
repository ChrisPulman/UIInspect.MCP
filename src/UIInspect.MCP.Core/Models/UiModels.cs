// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
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

/// <summary>Identifies one process instance and prevents PID reuse from inheriting consent.</summary>
/// <param name="ProcessId">Operating-system process identifier.</param>
/// <param name="StartedAtUtc">Process creation time.</param>
/// <param name="ProcessName">Executable process name.</param>
/// <param name="ExecutablePath">Best-effort executable path.</param>
/// <param name="SessionId">Windows logon session identifier.</param>
public sealed record ProcessIdentity(
    int ProcessId,
    DateTimeOffset StartedAtUtc,
    string ProcessName,
    string ExecutablePath,
    int SessionId);

/// <summary>A top-level window that may be attached.</summary>
/// <param name="Process">Owning process instance.</param>
/// <param name="WindowHandle">Native top-level window handle.</param>
/// <param name="Title">UI Automation window name.</param>
/// <param name="ClassName">Native or framework window class.</param>
/// <param name="FrameworkId">UI Automation framework identifier.</param>
/// <param name="IsEnabled">Whether the window is enabled.</param>
/// <param name="IsOffscreen">Whether the provider reports it as offscreen.</param>
public sealed record WindowDescriptor(
    ProcessIdentity Process,
    long WindowHandle,
    string Title,
    string ClassName,
    string FrameworkId,
    bool IsEnabled,
    bool IsOffscreen);

/// <summary>A device-independent element rectangle.</summary>
/// <param name="X">Left coordinate.</param>
/// <param name="Y">Top coordinate.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public sealed record UiRectangle(double X, double Y, double Width, double Height);

/// <summary>A flattened semantic UI Automation tree node.</summary>
/// <param name="ElementReference">Opaque, session-scoped element reference.</param>
/// <param name="ParentReference">Opaque parent reference, or null for the root.</param>
/// <param name="StablePath">Human-readable semantic path used to explain element identity.</param>
/// <param name="Depth">Depth relative to the attached root.</param>
/// <param name="ControlType">UI Automation control type.</param>
/// <param name="Name">Accessible element name; password elements are redacted.</param>
/// <param name="AutomationId">Provider automation identifier.</param>
/// <param name="ClassName">Provider class name.</param>
/// <param name="FrameworkId">Provider framework identifier.</param>
/// <param name="IsEnabled">Whether actions are enabled.</param>
/// <param name="IsOffscreen">Whether the provider reports the element as offscreen.</param>
/// <param name="IsPassword">Whether the provider marks the element as password content.</param>
/// <param name="Bounds">Bounding rectangle reported by UI Automation.</param>
/// <param name="SupportedPatterns">Supported semantic action patterns.</param>
public sealed record UiElementNode(
    string ElementReference,
    string? ParentReference,
    string StablePath,
    int Depth,
    string ControlType,
    string Name,
    string AutomationId,
    string ClassName,
    string FrameworkId,
    bool IsEnabled,
    bool IsOffscreen,
    bool IsPassword,
    UiRectangle Bounds,
    IReadOnlyList<string> SupportedPatterns);

/// <summary>A bounded point-in-time semantic UI tree.</summary>
/// <param name="SessionId">Owning automation session.</param>
/// <param name="Generation">Element-reference generation.</param>
/// <param name="Nodes">Flattened nodes in breadth-first order.</param>
/// <param name="Truncated">Whether the requested tree exceeded its depth or node budget.</param>
public sealed record UiTreeSnapshot(
    string SessionId,
    long Generation,
    IReadOnlyList<UiElementNode> Nodes,
    bool Truncated);

/// <summary>Details returned after an automation session is attached.</summary>
/// <param name="SessionId">Opaque session identifier.</param>
/// <param name="Target">Bound process instance.</param>
/// <param name="WindowHandle">Bound top-level window handle.</param>
/// <param name="ExpiresAtUtc">Consent expiry applied to the session.</param>
public sealed record AutomationSessionInfo(
    string SessionId,
    ProcessIdentity Target,
    long WindowHandle,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Public details of a consent grant.</summary>
/// <param name="ConsentId">Grant identifier.</param>
/// <param name="Target">Exact process instance.</param>
/// <param name="Capabilities">Granted capabilities.</param>
/// <param name="ExpiresAtUtc">Expiry.</param>
public sealed record ConsentGrantInfo(
    Guid ConsentId,
    ProcessIdentity Target,
    UiCapability Capabilities,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Outcome of a semantic UI action.</summary>
/// <param name="Action">Action name.</param>
/// <param name="ElementReference">Target element reference.</param>
/// <param name="ReferencesInvalidated">Whether a fresh inspection is required before another action.</param>
/// <param name="Message">Safe action summary.</param>
public sealed record ActionReceipt(
    string Action,
    string ElementReference,
    bool ReferencesInvalidated,
    string Message);

/// <summary>Safe operation error.</summary>
/// <param name="Code">Stable machine-readable code.</param>
/// <param name="Message">Safe user-facing message.</param>
/// <param name="RetryAfterMilliseconds">Optional retry delay for rate limits.</param>
public sealed record UiError(string Code, string Message, double? RetryAfterMilliseconds = null);

/// <summary>Uniform safe result envelope exposed by MCP tools.</summary>
/// <typeparam name="T">Payload type.</typeparam>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Data">Success payload.</param>
/// <param name="Error">Safe failure payload.</param>
public sealed record UiResult<T>(bool Success, T? Data, UiError? Error)
{
    /// <summary>Create a successful result.</summary>
    /// <param name="data">Payload.</param>
    /// <returns>Successful result.</returns>
    public static UiResult<T> Ok(T data) => new(true, data, null);

    /// <summary>Create a failed result.</summary>
    /// <param name="code">Stable code.</param>
    /// <param name="message">Safe message.</param>
    /// <param name="retryAfter">Optional retry delay.</param>
    /// <returns>Failed result.</returns>
    public static UiResult<T> Fail(string code, string message, TimeSpan? retryAfter = null) =>
        new(false, default, new UiError(code, message, retryAfter?.TotalMilliseconds));
}

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

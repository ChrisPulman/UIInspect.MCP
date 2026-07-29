// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.ComponentModel;
using ModelContextProtocol.Server;
using UIInspect.MCP.Core.Services;
using UIInspect.MCP.Server.Serialization;

namespace UIInspect.MCP.Server.Tools;

/// <summary>Consent-gated semantic Windows UI Automation MCP tools.</summary>
[McpServerToolType]
public sealed class UiInspectTools
{
    /// <summary>Identifier derived from the connected stdio transport rather than supplied by a tool caller.</summary>
    private const string ConnectedStdioClientId = "local-stdio";

    /// <summary>Default maximum descendant depth for the compact tree inspection overload.</summary>
    private const int DefaultInspectionDepth = 4;

    /// <summary>Default maximum flattened element count for the compact tree inspection overload.</summary>
    private const int DefaultInspectionNodeCount = 250;

    /// <summary>Gets the server-derived identity bound to the stdio transport.</summary>
    public string TransportClientId { get; } = ConnectedStdioClientId;

    /// <summary>List top-level windows on the current interactive Windows desktop.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <returns>A serialized discovery result.</returns>
    public static Task<string> DiscoverWindowsAsync(UiInspectService service) =>
        DiscoverWindowsAsync(service, CancellationToken.None);

    /// <summary>List top-level windows on the current interactive Windows desktop.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized discovery result.</returns>
    [McpServerTool(Name = "uiinspect_discover_windows")]
    [Description("List top-level UI Automation windows. This does not attach or grant access.")]
    public static async Task<string> DiscoverWindowsAsync(
        UiInspectService service,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(await service.DiscoverAsync(ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Show a trusted local-user consent prompt for a read-only process inspection.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="processId">Target Windows process identifier.</param>
    /// <returns>A serialized consent result.</returns>
    public static Task<string> RequestConsentAsync(UiInspectService service, int processId) =>
        RequestConsentAsync(service, processId, false, false);

    /// <summary>Show a trusted local-user consent prompt for one exact process instance.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="processId">Target Windows process identifier.</param>
    /// <param name="allowActions">Whether semantic interaction is requested.</param>
    /// <param name="allowKeyboard">Whether higher-risk logical keyboard input is requested.</param>
    /// <returns>A serialized consent result.</returns>
    public static Task<string> RequestConsentAsync(
        UiInspectService service,
        int processId,
        bool allowActions,
        bool allowKeyboard) =>
        RequestConsentAsync(service, processId, allowActions, allowKeyboard, CancellationToken.None);

    /// <summary>Show a trusted local-user consent prompt for one exact process instance.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="processId">Target Windows process identifier.</param>
    /// <param name="allowActions">Whether semantic interaction is requested.</param>
    /// <param name="allowKeyboard">Whether higher-risk logical keyboard input is requested.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized consent result.</returns>
    [McpServerTool(Name = "uiinspect_request_consent")]
    [Description("Show one trusted Windows approval dialog per exact process and server session. Repeated requests reuse the decision; capability expansion is denied without another dialog.")]
    public static async Task<string> RequestConsentAsync(
        UiInspectService service,
        [Description("Target Windows process ID.")] int processId,
        [Description("Request semantic interaction capability.")] bool allowActions,
        [Description("Request the higher-risk logical keyboard capability.")] bool allowKeyboard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.RequestConsentAsync(
                processId,
                allowActions,
                allowKeyboard,
                ConnectedStdioClientId,
                cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Attach an opaque session to a consented process without selecting a window.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="processId">Consented target Windows process identifier.</param>
    /// <returns>A serialized attach result.</returns>
    public static Task<string> AttachAsync(UiInspectService service, int processId) => AttachAsync(service, processId, null);

    /// <summary>Attach an opaque session to a consented process and optional top-level handle.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="processId">Consented target Windows process identifier.</param>
    /// <param name="windowHandle">Optional top-level native window handle returned by discovery.</param>
    /// <returns>A serialized attach result.</returns>
    public static Task<string> AttachAsync(UiInspectService service, int processId, long? windowHandle) =>
        AttachAsync(service, processId, windowHandle, CancellationToken.None);

    /// <summary>Attach an opaque session to a consented process and optional top-level handle.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="processId">Consented target Windows process identifier.</param>
    /// <param name="windowHandle">Optional top-level native window handle returned by discovery.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized attach result.</returns>
    [McpServerTool(Name = "uiinspect_attach")]
    [Description("Attach a UIA3 session after explicit consent. Supply null to let the server choose the target process window.")]
    public static async Task<string> AttachAsync(
        UiInspectService service,
        [Description("Consented target process ID.")] int processId,
        [Description("Optional native top-level window handle returned by discovery; null selects the process window.")] long? windowHandle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.AttachAsync(processId, windowHandle, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Return a bounded flattened semantic UI tree using default limits.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <returns>A serialized inspection result.</returns>
    public static Task<string> InspectTreeAsync(UiInspectService service, string sessionId) =>
        InspectTreeAsync(service, sessionId, DefaultInspectionDepth, DefaultInspectionNodeCount);

    /// <summary>Return a bounded flattened semantic UI tree and fresh opaque element references.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="maxDepth">Maximum descendant depth, from zero through twelve.</param>
    /// <param name="maxNodes">Maximum flattened elements, from one through one thousand.</param>
    /// <returns>A serialized inspection result.</returns>
    public static Task<string> InspectTreeAsync(
        UiInspectService service,
        string sessionId,
        int maxDepth,
        int maxNodes) =>
        InspectTreeAsync(service, sessionId, maxDepth, maxNodes, CancellationToken.None);

    /// <summary>Return a bounded flattened semantic UI tree and fresh opaque element references.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="maxDepth">Maximum descendant depth, from zero through twelve.</param>
    /// <param name="maxNodes">Maximum flattened elements, from one through one thousand.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized inspection result.</returns>
    [McpServerTool(Name = "uiinspect_inspect_tree")]
    [Description("Inspect a bounded UI Automation control tree. Password element names are redacted and control values are never returned.")]
    public static async Task<string> InspectTreeAsync(
        UiInspectService service,
        [Description("Opaque session ID returned by attach.")] string sessionId,
        [Description("Maximum descendant depth, from 0 through 12.")] int maxDepth,
        [Description("Maximum flattened elements, from 1 through 1000.")] int maxNodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.InspectAsync(sessionId, maxDepth, maxNodes, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Invoke a semantically resolved control.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <returns>A serialized action result.</returns>
    public static Task<string> InvokeAsync(UiInspectService service, string sessionId, string elementReference) =>
        InvokeAsync(service, sessionId, elementReference, CancellationToken.None);

    /// <summary>Invoke a semantically resolved control.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized action result.</returns>
    [McpServerTool(Name = "uiinspect_invoke")]
    [Description("Invoke an element through InvokePattern. A fresh tree inspection is required after success.")]
    public static async Task<string> InvokeAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.InvokeAsync(sessionId, elementReference, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Click a semantically resolved control.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <returns>A serialized action result.</returns>
    public static Task<string> ClickAsync(UiInspectService service, string sessionId, string elementReference) =>
        ClickAsync(service, sessionId, elementReference, CancellationToken.None);

    /// <summary>Click a semantically resolved control.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized action result.</returns>
    [McpServerTool(Name = "uiinspect_click")]
    [Description("Click the center of a resolved element. Prefer invoke when InvokePattern is available.")]
    public static async Task<string> ClickAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.ClickAsync(sessionId, elementReference, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Set an element through ValuePattern.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="value">Sensitive value to write; it is not written to the audit log.</param>
    /// <returns>A serialized action result.</returns>
    public static Task<string> SetValueAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string value) =>
        SetValueAsync(service, sessionId, elementReference, value, CancellationToken.None);

    /// <summary>Set an element through ValuePattern.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="value">Sensitive value to write; it is not written to the audit log.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized action result.</returns>
    [McpServerTool(Name = "uiinspect_set_value")]
    [Description("Set a string value through ValuePattern; read-only or unsupported elements fail without keyboard fallback.")]
    public static async Task<string> SetValueAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        [Description("Value is never written to the audit log.")] string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SetValueAsync(sessionId, elementReference, value, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Set text through ValuePattern.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="text">Sensitive text to write; it is not written to the audit log.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized action result.</returns>
    [McpServerTool(Name = "uiinspect_set_text")]
    [Description("Text-specific alias for set_value; uses ValuePattern and never logs the text.")]
    public static Task<string> SetTextAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string text,
        CancellationToken cancellationToken) =>
        SetValueAsync(service, sessionId, elementReference, text, cancellationToken);

    /// <summary>Set text through ValuePattern without request cancellation.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="text">Sensitive text to write; it is not written to the audit log.</param>
    /// <returns>A serialized action result.</returns>
    public static Task<string> SetTextAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string text) =>
        SetTextAsync(service, sessionId, elementReference, text, CancellationToken.None);

    /// <summary>Select an item through SelectionItemPattern.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <returns>A serialized action result.</returns>
    public static Task<string> SelectItemAsync(UiInspectService service, string sessionId, string elementReference) =>
        SelectItemAsync(service, sessionId, elementReference, CancellationToken.None);

    /// <summary>Select an item through SelectionItemPattern.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized action result.</returns>
    [McpServerTool(Name = "uiinspect_select_item")]
    [Description("Select an element through SelectionItemPattern.")]
    public static async Task<string> SelectItemAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SelectAsync(sessionId, elementReference, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Expand or collapse an element through ExpandCollapsePattern.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="expand">Whether the element should be expanded instead of collapsed.</param>
    /// <returns>A serialized action result.</returns>
    public static Task<string> ExpandCollapseAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        bool expand) =>
        ExpandCollapseAsync(service, sessionId, elementReference, expand, CancellationToken.None);

    /// <summary>Expand or collapse an element through ExpandCollapsePattern.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="expand">Whether the element should be expanded instead of collapsed.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized action result.</returns>
    [McpServerTool(Name = "uiinspect_expand_collapse")]
    [Description("Expand when expand=true, otherwise collapse, through ExpandCollapsePattern.")]
    public static async Task<string> ExpandCollapseAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        bool expand,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SetExpandedAsync(sessionId, elementReference, expand, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Focus a resolved element and send one allowlisted logical key.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="key">One allowlisted logical key name.</param>
    /// <returns>A serialized action result.</returns>
    public static Task<string> SendKeyAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string key) =>
        SendKeyAsync(service, sessionId, elementReference, key, CancellationToken.None);

    /// <summary>Focus a resolved element and send one allowlisted logical key.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="elementReference">Fresh opaque element reference returned by inspection.</param>
    /// <param name="key">One allowlisted logical key name.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized action result.</returns>
    [McpServerTool(Name = "uiinspect_send_key")]
    [Description("Send one allowlisted logical key after focusing the resolved element. Requires separate keyboard consent.")]
    public static async Task<string> SendKeyAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        [Description("One of ENTER, TAB, ESCAPE, SPACE, BACKSPACE, DELETE, HOME, END, PAGEUP, PAGEDOWN, or an arrow key.")] string key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SendKeyAsync(sessionId, elementReference, key, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Dispose an attached session.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <returns>A serialized close result.</returns>
    public static Task<string> CloseSessionAsync(UiInspectService service, string sessionId) =>
        CloseSessionAsync(service, sessionId, CancellationToken.None);

    /// <summary>Dispose an attached session.</summary>
    /// <param name="service">Coordinator that owns UI Automation access.</param>
    /// <param name="sessionId">Opaque session identifier returned by attach.</param>
    /// <param name="cancellationToken">Request cancellation token supplied by the MCP transport.</param>
    /// <returns>A serialized close result.</returns>
    [McpServerTool(Name = "uiinspect_close_session")]
    [Description("Close and dispose an attached automation session.")]
    public static async Task<string> CloseSessionAsync(
        UiInspectService service,
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.CloseSessionAsync(sessionId, ConnectedStdioClientId, cancellationToken).ConfigureAwait(false));
    }
}

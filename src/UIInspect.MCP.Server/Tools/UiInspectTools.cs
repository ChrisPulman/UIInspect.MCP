// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.ComponentModel;
using ModelContextProtocol.Server;
using UIInspect.MCP.Core.Services;
using UIInspect.MCP.Server.Serialization;

namespace UIInspect.MCP.Server.Tools;

/// <summary>Consent-gated semantic Windows UI Automation MCP tools.</summary>
[McpServerToolType]
public sealed class UiInspectTools
{
    /// <summary>List top-level windows on the current interactive Windows desktop.</summary>
    [McpServerTool(Name = "uiinspect_discover_windows")]
    [Description("List top-level UI Automation windows. This does not attach or grant access.")]
    public static async Task<string> DiscoverWindowsAsync(
        UiInspectService service,
        [Description("Stable local MCP client identifier used for consent scoping and redacted audit hashing.")] string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(await service.DiscoverAsync(clientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Show a trusted local-user consent prompt for one exact process instance.</summary>
    [McpServerTool(Name = "uiinspect_request_consent")]
    [Description("Show a trusted Windows approval dialog for one process instance. Inspection is always requested; actions and keyboard are opt-in.")]
    public static async Task<string> RequestConsentAsync(
        UiInspectService service,
        [Description("Target Windows process ID.")] int processId,
        [Description("Request semantic interaction capability.")] bool allowActions = false,
        [Description("Request the higher-risk logical keyboard capability.")] bool allowKeyboard = false,
        [Description("Stable local MCP client identifier.")] string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.RequestConsentAsync(
                processId,
                allowActions,
                allowKeyboard,
                clientId,
                cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Attach an opaque session to a consented process and optional top-level handle.</summary>
    [McpServerTool(Name = "uiinspect_attach")]
    [Description("Attach a UIA3 session after explicit consent. Optionally select one top-level native window handle.")]
    public static async Task<string> AttachAsync(
        UiInspectService service,
        [Description("Consented target process ID.")] int processId,
        [Description("Optional native top-level window handle returned by discovery.")] long? windowHandle = null,
        [Description("Stable local MCP client identifier.")] string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.AttachAsync(processId, windowHandle, clientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Return a bounded flattened semantic UI tree and fresh opaque element references.</summary>
    [McpServerTool(Name = "uiinspect_inspect_tree")]
    [Description("Inspect a bounded UI Automation control tree. Password element names are redacted and control values are never returned.")]
    public static async Task<string> InspectTreeAsync(
        UiInspectService service,
        [Description("Opaque session ID returned by attach.")] string sessionId,
        [Description("Maximum descendant depth, from 0 through 12.")] int maxDepth = 4,
        [Description("Maximum flattened elements, from 1 through 1000.")] int maxNodes = 250,
        [Description("Stable local MCP client identifier.")] string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.InspectAsync(
                sessionId,
                maxDepth,
                maxNodes,
                clientId,
                cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Invoke a semantically resolved control.</summary>
    [McpServerTool(Name = "uiinspect_invoke")]
    [Description("Invoke an element through InvokePattern. A fresh tree inspection is required after success.")]
    public static async Task<string> InvokeAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.InvokeAsync(sessionId, elementReference, clientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Click a semantically resolved control.</summary>
    [McpServerTool(Name = "uiinspect_click")]
    [Description("Click the center of a resolved element. Prefer invoke when InvokePattern is available.")]
    public static async Task<string> ClickAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.ClickAsync(sessionId, elementReference, clientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Set an element through ValuePattern.</summary>
    [McpServerTool(Name = "uiinspect_set_value")]
    [Description("Set a string value through ValuePattern; read-only or unsupported elements fail without keyboard fallback.")]
    public static async Task<string> SetValueAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        [Description("Value is never written to the audit log.")] string value,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SetValueAsync(
                sessionId,
                elementReference,
                value,
                clientId,
                cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Set text through ValuePattern.</summary>
    [McpServerTool(Name = "uiinspect_set_text")]
    [Description("Text-specific alias for set_value; uses ValuePattern and never logs the text.")]
    public static Task<string> SetTextAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string text,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default) =>
        SetValueAsync(service, sessionId, elementReference, text, clientId, cancellationToken);

    /// <summary>Select an item through SelectionItemPattern.</summary>
    [McpServerTool(Name = "uiinspect_select_item")]
    [Description("Select an element through SelectionItemPattern.")]
    public static async Task<string> SelectItemAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SelectAsync(sessionId, elementReference, clientId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Expand or collapse an element through ExpandCollapsePattern.</summary>
    [McpServerTool(Name = "uiinspect_expand_collapse")]
    [Description("Expand when expand=true, otherwise collapse, through ExpandCollapsePattern.")]
    public static async Task<string> ExpandCollapseAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        bool expand,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SetExpandedAsync(
                sessionId,
                elementReference,
                expand,
                clientId,
                cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Focus a resolved element and send one allowlisted logical key.</summary>
    [McpServerTool(Name = "uiinspect_send_key")]
    [Description("Send one allowlisted logical key after focusing the resolved element. Requires separate keyboard consent.")]
    public static async Task<string> SendKeyAsync(
        UiInspectService service,
        string sessionId,
        string elementReference,
        [Description("One of ENTER, TAB, ESCAPE, SPACE, BACKSPACE, DELETE, HOME, END, PAGEUP, PAGEDOWN, or an arrow key.")] string key,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.SendKeyAsync(
                sessionId,
                elementReference,
                key,
                clientId,
                cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Dispose an attached session.</summary>
    [McpServerTool(Name = "uiinspect_close_session")]
    [Description("Close and dispose an attached automation session.")]
    public static async Task<string> CloseSessionAsync(
        UiInspectService service,
        string sessionId,
        string clientId = "local-stdio",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        return JsonOutput.Serialize(
            await service.CloseSessionAsync(sessionId, clientId, cancellationToken).ConfigureAwait(false));
    }
}

// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using UIInspect.MCP.Core.Services;
using UIInspect.MCP.Server.Serialization;
using UIInspect.MCP.Server.Tools;

namespace UIInspect.MCP.Tests;

/// <summary>Tests MCP tool adapters, JSON output, and the composition root.</summary>
public sealed class ServerSurfaceTests
{
    /// <summary>JSON output uses stable web-style property names and omits nulls.</summary>
    [Test]
    public async Task Json_output_is_stable()
    {
        var json = JsonOutput.Serialize(new { SomeValue = 1, Missing = (string?)null });

        await Assert.That(json).Contains("\"someValue\": 1");
        await Assert.That(json).DoesNotContain("missing");
    }

    /// <summary>Every MCP adapter delegates to the covered coordinator contract.</summary>
    [Test]
    public async Task Tools_delegate_every_mvp_operation()
    {
        await using var harness = new ServiceHarness();
        var discover = await UiInspectTools.DiscoverWindowsAsync(harness.Service);
        var consent = await UiInspectTools.RequestConsentAsync(
            harness.Service,
            harness.Target.ProcessId,
            true,
            true);
        var attach = await UiInspectTools.AttachAsync(
            harness.Service,
            harness.Target.ProcessId,
            null,
            "local-stdio");
        using var attachJson = System.Text.Json.JsonDocument.Parse(attach);
        var sessionId = attachJson.RootElement
            .GetProperty("data")
            .GetProperty("sessionId")
            .GetString()!;

        var inspect = await UiInspectTools.InspectTreeAsync(harness.Service, sessionId);
        var invoke = await UiInspectTools.InvokeAsync(harness.Service, sessionId, "e");
        var click = await UiInspectTools.ClickAsync(harness.Service, sessionId, "e");
        var value = await UiInspectTools.SetValueAsync(harness.Service, sessionId, "e", "value");
        var text = await UiInspectTools.SetTextAsync(harness.Service, sessionId, "e", "text");
        var select = await UiInspectTools.SelectItemAsync(harness.Service, sessionId, "e");
        var expand = await UiInspectTools.ExpandCollapseAsync(harness.Service, sessionId, "e", true);
        var key = await UiInspectTools.SendKeyAsync(harness.Service, sessionId, "e", "ENTER");
        var close = await UiInspectTools.CloseSessionAsync(harness.Service, sessionId);

        foreach (var json in new[]
        {
            discover,
            consent,
            attach,
            inspect,
            invoke,
            click,
            value,
            text,
            select,
            expand,
            key,
            close,
        })
        {
            await Assert.That(json).Contains("\"success\": true");
        }
    }

    /// <summary>Tool service arguments fail fast instead of producing null-reference errors.</summary>
    [Test]
    public async Task Tools_reject_null_services()
    {
        await Assert.That(async () => { _ = await UiInspectTools.DiscoverWindowsAsync(null!); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.RequestConsentAsync(null!, 1); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.AttachAsync(null!, 1); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.InspectTreeAsync(null!, "s"); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.InvokeAsync(null!, "s", "e"); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.ClickAsync(null!, "s", "e"); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.SetValueAsync(null!, "s", "e", "v"); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.SelectItemAsync(null!, "s", "e"); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.ExpandCollapseAsync(null!, "s", "e", true); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.SendKeyAsync(null!, "s", "e", "ENTER"); }).Throws<ArgumentNullException>();
        await Assert.That(async () => { _ = await UiInspectTools.CloseSessionAsync(null!, "s"); }).Throws<ArgumentNullException>();
    }

    /// <summary>The production host registers the coordinator and MCP services.</summary>
    [Test]
    public async Task Production_host_builds_with_required_services()
    {
        using var host = UIInspect.MCP.Server.Program.CreateHost([]);
        var service = host.Services.GetRequiredService<UiInspectService>();
        var options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var serverInfo = options.ServerInfo ?? throw new InvalidOperationException("Server information was not configured.");

        await Assert.That(service).IsNotNull();
        await Assert.That(serverInfo.Name).IsEqualTo("uiinspect-mcp");
        await Assert.That(serverInfo.Title).IsEqualTo("UIInspect MCP Server");
        await Assert.That(serverInfo.Description).Contains("semantic Windows UI Automation");
    }

    /// <summary>The controlled MCP registry and bundled skill document every public tool.</summary>
    [Test]
    public async Task Tool_manifest_and_documentation_are_complete()
    {
        var expectedTools = new[]
        {
            "uiinspect_discover_windows",
            "uiinspect_request_consent",
            "uiinspect_attach",
            "uiinspect_inspect_tree",
            "uiinspect_invoke",
            "uiinspect_click",
            "uiinspect_set_value",
            "uiinspect_set_text",
            "uiinspect_select_item",
            "uiinspect_expand_collapse",
            "uiinspect_send_key",
            "uiinspect_close_session",
        };
        var registeredTools = typeof(UiInspectTools)
            .GetMethods()
            .Select(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), false).SingleOrDefault())
            .OfType<McpServerToolAttribute>()
            .Select(attribute => attribute.Name!)
            .ToArray();
        var root = FindWorkspaceRoot();
        var readme = await File.ReadAllTextAsync(Path.Combine(root, "README.md"));
        var skill = await File.ReadAllTextAsync(Path.Combine(root, "skills", "uiinspect", "SKILL.md"));
        var packages = await File.ReadAllTextAsync(Path.Combine(root, "Directory.Packages.props"));

        await Assert.That(registeredTools).IsEquivalentTo(expectedTools);
        foreach (var tool in expectedTools)
        {
            await Assert.That(readme).Contains(tool);
            await Assert.That(skill).Contains(tool);
        }

        await Assert.That(packages).Contains("ModelContextProtocol\" Version=\"1.4.1");
        await Assert.That(packages).Contains("FlaUI.UIA3\" Version=\"5.0.0");
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UIInspect.MCP.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("UIInspect.MCP workspace root was not found.");
    }
}

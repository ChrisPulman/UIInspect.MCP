// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using UIInspect.MCP.Core.Services;
using UIInspect.MCP.Server.Serialization;
using UIInspect.MCP.Server.Tools;
using UIInspect.MCP.Windows.DependencyInjection;

namespace UIInspect.MCP.Tests;

/// <summary>Tests MCP tool adapters, JSON output, and the composition root.</summary>
public sealed class ServerSurfaceTests
{
    /// <summary>The directory that contains packaged image assets.</summary>
    private const string ImagesDirectoryName = "images";

    /// <summary>The JSON property containing the server session identifier.</summary>
    private const string SessionIdPropertyName = "sessionId";

    /// <summary>JSON output uses stable web-style property names and omits nulls.</summary>
    /// <returns>A task that verifies JSON serialization.</returns>
    [Test]
    public async Task Json_output_is_stable()
    {
        var json = JsonOutput.Serialize(new JsonTestPayload(1, null));

        await Assert.That(json).Contains("\"someValue\": 1");
        await Assert.That(json).DoesNotContain("missing");
    }

    /// <summary>Every MCP adapter delegates to the covered coordinator contract.</summary>
    /// <returns>A task that verifies every MVP tool adapter.</returns>
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
            harness.Target.ProcessId);
        using var attachJson = System.Text.Json.JsonDocument.Parse(attach);
        var sessionId = attachJson.RootElement
            .GetProperty("data")
            .GetProperty(SessionIdPropertyName)
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
    /// <returns>A task that verifies null argument handling.</returns>
    [Test]
    public async Task Tools_reject_null_services()
    {
        await Assert.That(static async () => { _ = await UiInspectTools.DiscoverWindowsAsync(null!); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.RequestConsentAsync(null!, 1); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.AttachAsync(null!, 1); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.InspectTreeAsync(null!, "s"); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.InvokeAsync(null!, "s", "e"); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.ClickAsync(null!, "s", "e"); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.SetValueAsync(null!, "s", "e", "v"); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.SelectItemAsync(null!, "s", "e"); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.ExpandCollapseAsync(null!, "s", "e", true); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.SendKeyAsync(null!, "s", "e", "ENTER"); }).Throws<ArgumentNullException>();
        await Assert.That(static async () => { _ = await UiInspectTools.CloseSessionAsync(null!, "s"); }).Throws<ArgumentNullException>();
    }

    /// <summary>The production host registers the coordinator and MCP services.</summary>
    /// <returns>A task that verifies service registration.</returns>
    [Test]
    public async Task Production_host_builds_with_required_services()
    {
        var defaultServices = new ServiceCollection();
        _ = defaultServices.AddWindowsUiInspect();
        using var host = UIInspect.MCP.Server.Program.CreateHost([]);
        var service = host.Services.GetRequiredService<UiInspectService>();
        var options = host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var serverInfo = options.ServerInfo ?? throw new InvalidOperationException("Server information was not configured.");
        UiInspectTools tools = new();

        await Assert.That(defaultServices.Count).IsGreaterThan(0);
        await Assert.That(service).IsNotNull();
        await Assert.That(tools.TransportClientId).IsEqualTo("local-stdio");
        await Assert.That(serverInfo.Name).IsEqualTo("uiinspect-mcp");
        await Assert.That(serverInfo.Title).IsEqualTo("UIInspect MCP Server");
        await Assert.That(serverInfo.Description).Contains("semantic Windows UI Automation");
    }

    /// <summary>The controlled MCP registry and bundled skill document every public tool.</summary>
    /// <returns>A task that verifies the public MCP surface.</returns>
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
        var (registeredTools, exposesCallerControlledClientId) = CollectToolMetadata();
        var root = FindWorkspaceRoot();
        await Assert.That(registeredTools).IsEquivalentTo(expectedTools);
        await Assert.That(exposesCallerControlledClientId).IsFalse();
        await VerifyBundledDocumentationAsync(root, expectedTools);
    }

    /// <summary>Collects registered MCP tools and detects caller-controlled identities.</summary>
    /// <returns>The registered tool names and whether a client identifier is public.</returns>
    private static (List<string> RegisteredTools, bool ExposesCallerControlledClientId) CollectToolMetadata()
    {
        var registeredTools = new List<string>();
        foreach (var method in typeof(UiInspectTools).GetMethods())
        {
            foreach (var attribute in method.GetCustomAttributes(typeof(McpServerToolAttribute), false))
            {
                if (attribute is McpServerToolAttribute toolAttribute)
                {
                    registeredTools.Add(toolAttribute.Name!);
                }
            }

            foreach (var parameter in method.GetParameters())
            {
                if (string.Equals(parameter.Name, "clientId", StringComparison.Ordinal))
                {
                    return (registeredTools, true);
                }
            }
        }

        return (registeredTools, false);
    }

    /// <summary>Verifies repository documentation, package metadata, and visual assets.</summary>
    /// <param name="root">The workspace root directory.</param>
    /// <param name="expectedTools">The expected public tool names.</param>
    /// <returns>A task that verifies bundled repository assets.</returns>
    private static async Task VerifyBundledDocumentationAsync(string root, IReadOnlyList<string> expectedTools)
    {
        var readme = await File.ReadAllTextAsync(Path.Combine(root, "README.md"));
        var skill = await File.ReadAllTextAsync(Path.Combine(root, "skills", "uiinspect", "SKILL.md"));
        var packages = await File.ReadAllTextAsync(Path.Combine(root, "Directory.Packages.props"));
        var buildProperties = await File.ReadAllTextAsync(Path.Combine(root, "Directory.Build.props"));
        var buildDefinition = await File.ReadAllTextAsync(Path.Combine(root, "build", "Build.cs"));
        var solution = await File.ReadAllTextAsync(Path.Combine(root, "src", "UIInspect.MCP.slnx"));
        var manifestSource = await File.ReadAllTextAsync(Path.Combine(root, ".mcp", "server.json"));
        var serverProject = await File.ReadAllTextAsync(Path.Combine(root, "src", "UIInspect.MCP.Server", "UIInspect.MCP.Server.csproj"));
        var windowsProject = await File.ReadAllTextAsync(Path.Combine(root, "src", "UIInspect.MCP.Windows", "UIInspect.MCP.Windows.csproj"));
        var serverAssemblyInfo = await File.ReadAllTextAsync(Path.Combine(root, "src", "UIInspect.MCP.Server", "Properties", "AssemblyInfo.cs"));
        var windowsAssemblyInfo = await File.ReadAllTextAsync(Path.Combine(root, "src", "UIInspect.MCP.Windows", "Properties", "AssemblyInfo.cs"));
        using var manifest = System.Text.Json.JsonDocument.Parse(manifestSource);
        var manifestVersion = manifest.RootElement.GetProperty("version").GetString();
        var packageVersion = manifest.RootElement.GetProperty(nameof(packages))[0].GetProperty("version").GetString();

        foreach (var tool in expectedTools)
        {
            await Assert.That(readme).Contains(tool);
            await Assert.That(skill).Contains(tool);
        }

        await Assert.That(packages).Contains("ModelContextProtocol\" Version=\"1.4.1");
        await Assert.That(packages).Contains("FlaUI.UIA3\" Version=\"5.0.0");
        await Assert.That(packages).Contains("MinVer\" Version=\"7.0.0");
        await Assert.That(packages).DoesNotContain("Nerdbank.GitVersioning");
        await Assert.That(buildProperties).Contains("<PackageReference Include=\"MinVer\" PrivateAssets=\"all\" />");
        await Assert.That(buildProperties).Contains("<MinVerTagPrefix>v</MinVerTagPrefix>");
        await Assert.That(buildDefinition).Contains("Target SynchronizeVersion");
        await Assert.That(buildDefinition).Contains("-getProperty:MinVerVersion,PackageVersion");
        await Assert.That(buildDefinition).Contains("Environment.SetEnvironmentVariable(\"MinVerVersionOverride\", _minVerVersion)");
        await Assert.That(solution).Contains("<Project Path=\"../build/_build.csproj\" />");
        await Assert.That(manifestVersion).IsNotNull();
        await Assert.That(packageVersion).IsEqualTo(manifestVersion);
        await Assert.That(readme).Contains($"dnx UIInspect.MCP.Server@{manifestVersion} --yes");
        await Assert.That(readme).Contains("images/ReadmeHero.png");
        await Assert.That(readme).Contains("<!-- mcp-name: io.github.chrispulman/uiinspect-mcp -->");
        await Assert.That(buildProperties).Contains("<PackageIcon>IconNuget.png</PackageIcon>");
        await Assert.That(serverProject).Contains("<TargetFramework>net10.0</TargetFramework>");
        await Assert.That(serverProject).DoesNotContain("<PackageVersion>");
        await Assert.That(serverProject).Contains("<PackageReadmeFile>README.md</PackageReadmeFile>");
        await Assert.That(windowsProject).Contains("<TargetFramework>net10.0</TargetFramework>");
        await Assert.That(windowsProject).DoesNotContain("<UseWindowsForms>");
        await Assert.That(serverAssemblyInfo).Contains("SupportedOSPlatform(\"windows\")");
        await Assert.That(windowsAssemblyInfo).Contains("SupportedOSPlatform(\"windows\")");
        await Assert.That(File.Exists(Path.Combine(root, ImagesDirectoryName, "IconNuget.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, ImagesDirectoryName, "GitHubLogo.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(root, ImagesDirectoryName, "ReadmeHero.png"))).IsTrue();
    }

    /// <summary>Finds the workspace root from the test output directory.</summary>
    /// <returns>The absolute workspace root path.</returns>
    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "UIInspect.MCP.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("UIInspect.MCP workspace root was not found.");
    }

    /// <summary>Serializable payload used to verify JSON naming and null behavior.</summary>
    /// <param name="SomeValue">The value that must be serialized.</param>
    /// <param name="Missing">The null value that must be omitted.</param>
    private sealed record JsonTestPayload(int SomeValue, string? Missing);
}

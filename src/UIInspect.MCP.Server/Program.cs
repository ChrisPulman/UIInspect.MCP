// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using UIInspect.MCP.Server.Tools;
using UIInspect.MCP.Windows.DependencyInjection;

namespace UIInspect.MCP.Server;

/// <summary>Entry point for the UIInspect MCP server.</summary>
public static class Program
{
    /// <summary>Create the configured stdio host.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Configured host.</returns>
    public static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        _ = builder.Logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        var auditPath = Environment.GetEnvironmentVariable("UIINSPECT_AUDIT_PATH");
        _ = builder.Services.AddWindowsUiInspect(null, auditPath);
        _ = builder.Services
            .AddMcpServer(
                static options => options.ServerInfo = new Implementation
                {
                    Name = "uiinspect-mcp",
                    Version = typeof(Program).Assembly.GetName().Version!.ToString(),
                    Title = "UIInspect MCP Server",
                    Description = "Consent-gated semantic Windows UI Automation inspection and action tools.",
                })
            .WithStdioServerTransport()
            .WithTools<UiInspectTools>();
        return builder.Build();
    }

    /// <summary>Run the stdio MCP server.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Zero on normal completion; one when explicit skill installation fails.</returns>
    [ExcludeFromCodeCoverage(Justification = "The process entry point delegates entirely to the covered CreateHost composition root.")]
    public static async Task<int> Main(string[] args)
    {
        if (CodexSkillInstaller.IsInstallRequested(args))
        {
            var result = CodexSkillInstaller.InstallBundledSkill(
                createCodexHome: true,
                overwrite: CodexSkillInstaller.IsForceRequested(args));
            await Console.Error.WriteLineAsync(result.Message).ConfigureAwait(false);
            return result.Success ? 0 : 1;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("UIInspect.MCP requires Windows and an interactive desktop.");
        }

        _ = CodexSkillInstaller.TryAutoInstall(Console.Error);
        await CreateHost(args).RunAsync().ConfigureAwait(false);
        return 0;
    }
}

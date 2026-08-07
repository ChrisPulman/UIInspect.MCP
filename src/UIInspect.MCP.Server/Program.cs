// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Server.Tools;
using UIInspect.MCP.Windows.DependencyInjection;
using UIInspect.MCP.Windows.Security;

namespace UIInspect.MCP.Server;

/// <summary>Entry point for the UIInspect MCP server.</summary>
public static class Program
{
    /// <summary>Private command used by the trusted broker child process.</summary>
    private const string RunBrokerOption = "--run-unattended-broker";

    /// <summary>User-facing command that requests a fixed-duration approval.</summary>
    private const string AuthorizeOption = "--authorize-unattended";

    /// <summary>Exit code used for malformed command-line arguments.</summary>
    private const int InvalidArgumentsExitCode = 2;

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
    [STAThread]
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

        var administrativeResult = await TryRunAdministrativeCommandAsync(args).ConfigureAwait(false);
        if (administrativeResult.HasValue)
        {
            return administrativeResult.Value;
        }

        _ = CodexSkillInstaller.TryAutoInstall(Console.Error);
        await CreateHost(args).RunAsync().ConfigureAwait(false);
        return 0;
    }

    /// <summary>Run one private broker or user-facing approval management command.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>An exit code when an administrative command was present; otherwise <see langword="null"/>.</returns>
    private static async Task<int?> TryRunAdministrativeCommandAsync(string[] args)
    {
        if (HasOption(args, RunBrokerOption))
        {
            return await RunBrokerCommandAsync(args).ConfigureAwait(false);
        }

        if (HasOption(args, AuthorizeOption))
        {
            return await RunAuthorizeCommandAsync(args).ConfigureAwait(false);
        }

        if (args.Contains("--revoke-unattended", StringComparer.Ordinal))
        {
            var revoked = await new WindowsUnattendedApprovalAuthorizer()
                .RevokeAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await Console.Out.WriteLineAsync(
                revoked ? "UIInspect unattended approval was revoked." : "No active unattended approval was found.")
                .ConfigureAwait(false);
            return revoked ? 0 : 1;
        }

        if (args.Contains("--unattended-status", StringComparer.Ordinal))
        {
            var lease = await new WindowsUnattendedApprovalAuthorizer()
                .GetStatusAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await Console.Out.WriteLineAsync(
                lease is null
                    ? "UIInspect unattended approval is not active."
                    : $"UIInspect unattended approval is active until {lease.ExpiresAtUtc:O} with {lease.Capabilities}.")
                .ConfigureAwait(false);
            return lease is null ? 1 : 0;
        }

        return null;
    }

    /// <summary>Run the private broker child process.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The broker exit code.</returns>
    private static async Task<int> RunBrokerCommandAsync(string[] args)
    {
        if (!TryReadSupportedHours(args, RunBrokerOption, out var hours))
        {
            return await WriteInvalidHoursAsync(RunBrokerOption).ConfigureAwait(false);
        }

        using var broker = new WindowsUnattendedApprovalBroker();
        return await broker.RunAsync(hours, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Request a trusted unattended approval lease.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The approval command exit code.</returns>
    private static async Task<int> RunAuthorizeCommandAsync(string[] args)
    {
        if (!TryReadSupportedHours(args, AuthorizeOption, out var hours))
        {
            return await WriteInvalidHoursAsync(AuthorizeOption).ConfigureAwait(false);
        }

        var lease = await WindowsUnattendedApprovalLauncher.StartAsync(hours, CancellationToken.None).ConfigureAwait(false);
        if (lease is null)
        {
            await Console.Error.WriteLineAsync("Unattended UIInspect approval was denied or timed out.").ConfigureAwait(false);
            return 1;
        }

        await Console.Out.WriteLineAsync(
            $"UIInspect unattended approval is active until {lease.ExpiresAtUtc:O} with {lease.Capabilities}.")
            .ConfigureAwait(false);
        return 0;
    }

    /// <summary>Write the supported-duration command usage.</summary>
    /// <param name="option">The malformed command option.</param>
    /// <returns>The invalid-arguments exit code.</returns>
    private static async Task<int> WriteInvalidHoursAsync(string option)
    {
        await Console.Error.WriteLineAsync($"{option} requires one approval window: 1, 2, 5, 8, 12, or 24 hours.")
            .ConfigureAwait(false);
        return InvalidArgumentsExitCode;
    }

    /// <summary>Read one required whole-hour command option.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="option">Option name.</param>
    /// <param name="hours">Parsed hours.</param>
    /// <returns><see langword="true"/> when the option is present and valid.</returns>
    private static bool TryReadSupportedHours(string[] args, string option, out int hours)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (string.Equals(args[index], option, StringComparison.Ordinal)
                && index + 1 < args.Length
                && int.TryParse(
                    args[index + 1],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out hours))
            {
                return IsSupported(hours);
            }

            var prefix = $"{option}=";
            if (args[index].StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(
                    args[index].AsSpan(prefix.Length),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out hours))
            {
                return IsSupported(hours);
            }
        }

        hours = 0;
        return false;
    }

    /// <summary>Determine whether an option is present in split or equals form.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="option">Option name.</param>
    /// <returns><see langword="true"/> when the option is present.</returns>
    private static bool HasOption(string[] args, string option)
    {
        var prefix = $"{option}=";
        foreach (var argument in args)
        {
            if (string.Equals(argument, option, StringComparison.Ordinal)
                || argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determine whether a parsed approval duration is supported.</summary>
    /// <param name="hours">Parsed whole-hour duration.</param>
    /// <returns><see langword="true"/> when the duration is one of the fixed choices.</returns>
    private static bool IsSupported(int hours) => UnattendedApprovalDurations.IsSupported(hours);
}

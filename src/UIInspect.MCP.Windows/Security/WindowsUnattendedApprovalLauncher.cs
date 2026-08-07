// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Reflection;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;

namespace UIInspect.MCP.Windows.Security;

/// <summary>Starts the trusted broker mode in a separate hidden console process.</summary>
public static class WindowsUnattendedApprovalLauncher
{
    /// <summary>Delay between broker status probes.</summary>
    private const int BrokerPollMilliseconds = 200;

    /// <summary>Maximum time to wait for the local user to approve or deny the child broker.</summary>
    private static readonly TimeSpan BrokerStartupTimeout = TimeSpan.FromMinutes(2.25);

    /// <summary>Start a broker and wait until its approved lease becomes visible.</summary>
    /// <param name="hours">One supported fixed approval window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active lease, or <see langword="null"/> when denied or timed out.</returns>
    public static async Task<UnattendedApprovalLease?> StartAsync(
        int hours,
        CancellationToken cancellationToken)
    {
        _ = UnattendedApprovalDurations.FromHours(hours);
        var authorizer = new WindowsUnattendedApprovalAuthorizer();
        var active = await authorizer.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (active is not null)
        {
            return active;
        }

        using var child = StartBrokerProcess(hours);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(BrokerStartupTimeout);
        try
        {
            while (!timeout.IsCancellationRequested && !child.HasExited)
            {
                active = await authorizer.GetStatusAsync(timeout.Token).ConfigureAwait(false);
                if (active is not null)
                {
                    return active;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(BrokerPollMilliseconds), timeout.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return null;
        }

        return null;
    }

    /// <summary>Start the current entry assembly in private broker mode.</summary>
    /// <param name="hours">Approval hours.</param>
    /// <returns>Started child process.</returns>
    private static Process StartBrokerProcess(int hours)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current process path is unavailable.");
        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("The entry assembly path is unavailable.");
        var startInfo = new ProcessStartInfo { FileName = processPath, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, };
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(entryAssemblyPath);
        }

        startInfo.ArgumentList.Add("--run-unattended-broker");
        startInfo.ArgumentList.Add(hours.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The unattended approval broker could not be started.");
    }
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Security;

/// <summary>Shows a trusted local Windows dialog outside the inspected application.</summary>
[ExcludeFromCodeCoverage(Justification = "The native modal dialog is covered by manual security verification; policy behavior is tested through IUserConsentPrompt fakes.")]
public sealed class TrustedWindowsConsentPrompt : IUserConsentPrompt
{
    /// <inheritdoc/>
    public async ValueTask<bool> RequestAsync(
        ProcessIdentity target,
        UiCapability capabilities,
        string clientId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        cancellationToken.ThrowIfCancellationRequested();

        using var targetProcess = TryOpenExactProcess(target);
        if (targetProcess is null)
        {
            return false;
        }

        using var targetLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        targetProcess.EnableRaisingEvents = true;
        void TargetExited(object? _, EventArgs eventArgs)
        {
            _ = eventArgs;
            targetLifetime.Cancel();
        }

        targetProcess.Exited += TargetExited;
        try
        {
            var path = string.IsNullOrWhiteSpace(target.ExecutablePath)
                ? "(path unavailable)"
                : target.ExecutablePath;
            var message = $"""
                Allow the connected local UIInspect MCP client to access this process?

                Process: {target.ProcessName}
                PID: {target.ProcessId}
                Path: {path}
                Capabilities: {capabilities}

                UIInspect uses Windows UI Automation only. This decision is retained only for
                this server session and remains bound to this exact process and capability set.
                The request closes automatically if the process exits or the approval times out.
                """;
            return await TrustedWindowsDialog
                .ShowAsync(message, "UIInspect MCP consent", targetLifetime.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            targetProcess.Exited -= TargetExited;
        }
    }

    /// <summary>Open and verify the exact target process before displaying a prompt.</summary>
    /// <param name="target">Expected process identity.</param>
    /// <returns>The live process, or <see langword="null"/> when it changed or exited.</returns>
    private static Process? TryOpenExactProcess(ProcessIdentity target)
    {
        try
        {
            var process = Process.GetProcessById(target.ProcessId);
            var startedAtUtc = new DateTimeOffset(process.StartTime.ToUniversalTime());
            if (startedAtUtc != target.StartedAtUtc || process.SessionId != target.SessionId)
            {
                process.Dispose();
                return null;
            }

            return process;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}

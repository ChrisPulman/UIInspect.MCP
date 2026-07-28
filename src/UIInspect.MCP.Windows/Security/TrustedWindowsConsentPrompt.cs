// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
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

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(
            () =>
            {
                var path = string.IsNullOrWhiteSpace(target.ExecutablePath)
                    ? "(path unavailable)"
                    : target.ExecutablePath;
                var message =
                    $"Allow UIInspect MCP client '{clientId}' to access this process?\n\n" +
                    $"Process: {target.ProcessName}\n" +
                    $"PID: {target.ProcessId}\n" +
                    $"Path: {path}\n" +
                    $"Capabilities: {capabilities}\n\n" +
                    "UIInspect uses Windows UI Automation only. Approval expires automatically.";
                var result = MessageBox.Show(
                    message,
                    "UIInspect MCP consent",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2,
                    MessageBoxOptions.DefaultDesktopOnly);
                _ = completion.TrySetResult(result == DialogResult.Yes);
            })
        {
            IsBackground = true,
            Name = "UIInspect consent prompt",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}

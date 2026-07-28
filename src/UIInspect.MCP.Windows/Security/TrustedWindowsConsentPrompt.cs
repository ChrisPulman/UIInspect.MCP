// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Security;

/// <summary>Shows a trusted local Windows dialog outside the inspected application.</summary>
[ExcludeFromCodeCoverage(Justification = "The native modal dialog is covered by manual security verification; policy behavior is tested through IUserConsentPrompt fakes.")]
public sealed partial class TrustedWindowsConsentPrompt : IUserConsentPrompt
{
    /// <summary>Native <c>IDYES</c> result value returned by <c>MessageBoxW</c>.</summary>
    private const int DialogResultYes = 6;

    /// <summary>Displays affirmative and negative choices.</summary>
    private const uint MessageBoxYesNo = 0x00000004;

    /// <summary>Displays the Windows warning icon.</summary>
    private const uint MessageBoxIconWarning = 0x00000030;

    /// <summary>Sets the negative choice as the default to make denial safe by default.</summary>
    private const uint MessageBoxDefaultButton2 = 0x00000100;

    /// <summary>Brings the trusted consent prompt to the foreground.</summary>
    private const uint MessageBoxSetForeground = 0x00010000;

    /// <summary>Uses the interactive desktop rather than the inspected application window.</summary>
    private const uint MessageBoxDefaultDesktopOnly = 0x00020000;

    /// <summary>Keeps the consent prompt visible over the inspected application.</summary>
    private const uint MessageBoxTopMost = 0x00040000;

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
                var message = $"""
                    Allow the connected local UIInspect MCP client to access this process?

                    Process: {target.ProcessName}
                    PID: {target.ProcessId}
                    Path: {path}
                    Capabilities: {capabilities}

                    UIInspect uses Windows UI Automation only. Approval expires automatically.
                    """;
                var result = MessageBox(
                    0,
                    message,
                    "UIInspect MCP consent",
                    MessageBoxYesNo
                    | MessageBoxIconWarning
                    | MessageBoxDefaultButton2
                    | MessageBoxSetForeground
                    | MessageBoxDefaultDesktopOnly
                    | MessageBoxTopMost);
                _ = completion.TrySetResult(result == DialogResultYes);
            }) { IsBackground = true, Name = "UIInspect consent prompt", };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Displays the native, trusted consent dialog from the Windows system library.</summary>
    /// <param name="windowHandle">Parent window handle; zero selects no application-owned parent.</param>
    /// <param name="text">Dialog body.</param>
    /// <param name="caption">Dialog title.</param>
    /// <param name="type">Native message-box flags.</param>
    /// <returns>The native dialog result.</returns>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(nint windowHandle, string text, string caption, uint type);
}

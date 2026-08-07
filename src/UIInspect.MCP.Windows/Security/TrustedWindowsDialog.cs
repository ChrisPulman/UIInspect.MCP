// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace UIInspect.MCP.Windows.Security;

/// <summary>Shows a trusted, cancellable, fail-closed Windows confirmation dialog.</summary>
[ExcludeFromCodeCoverage(Justification = "Native dialog rendering and cancellation are manually verified; policy is covered through prompt fakes.")]
internal static partial class TrustedWindowsDialog
{
    /// <summary>Native affirmative result.</summary>
    private const int DialogResultYes = 6;

    /// <summary>Native close-window message.</summary>
    private const uint CloseWindowMessage = 0x0010;

    /// <summary>Displays affirmative and negative choices.</summary>
    private const uint MessageBoxYesNo = 0x00000004;

    /// <summary>Displays the Windows warning icon.</summary>
    private const uint MessageBoxIconWarning = 0x00000030;

    /// <summary>Sets denial as the default choice.</summary>
    private const uint MessageBoxDefaultButton2 = 0x00000100;

    /// <summary>Brings the prompt to the foreground.</summary>
    private const uint MessageBoxSetForeground = 0x00010000;

    /// <summary>Uses the interactive desktop.</summary>
    private const uint MessageBoxDefaultDesktopOnly = 0x00020000;

    /// <summary>Keeps the prompt visible over target applications.</summary>
    private const uint MessageBoxTopMost = 0x00040000;

    /// <summary>Delay between cancellation window discovery attempts.</summary>
    private const int CancellationPollMilliseconds = 25;

    /// <summary>Show one confirmation that closes when cancelled.</summary>
    /// <param name="message">Dialog body.</param>
    /// <param name="title">Dialog title prefix.</param>
    /// <param name="cancellationToken">Cancellation or timeout signal.</param>
    /// <returns><see langword="true"/> only when the user selected Yes.</returns>
    internal static async ValueTask<bool> ShowAsync(
        string message,
        string title,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        cancellationToken.ThrowIfCancellationRequested();

        var caption = $"{title} [{Guid.NewGuid():N}]";
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(
            () =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _ = completion.TrySetCanceled(cancellationToken);
                    return;
                }

                var result = MessageBox(
                    0,
                    message,
                    caption,
                    MessageBoxYesNo
                    | MessageBoxIconWarning
                    | MessageBoxDefaultButton2
                    | MessageBoxSetForeground
                    | MessageBoxDefaultDesktopOnly
                    | MessageBoxTopMost);
                _ = completion.TrySetResult(result);
            }) { IsBackground = true, Name = "UIInspect consent prompt", };
        thread.SetApartmentState(ApartmentState.STA);

        await using var registration = cancellationToken.Register(
            static state => _ = CloseWhenAvailableAsync((DialogCancellationState)state!),
            new DialogCancellationState(caption, completion.Task));
        thread.Start();

        var dialogResult = await completion.Task.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return dialogResult == DialogResultYes;
    }

    /// <summary>Close the uniquely titled dialog as soon as its window exists.</summary>
    /// <param name="state">Cancellation state.</param>
    /// <returns>Completion after the dialog closes.</returns>
    private static async Task CloseWhenAvailableAsync(DialogCancellationState state)
    {
        while (!state.Completion.IsCompleted)
        {
            var window = FindWindow(null, state.Caption);
            if (window != 0)
            {
                _ = PostMessage(window, CloseWindowMessage, 0, 0);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(CancellationPollMilliseconds)).ConfigureAwait(false);
        }
    }

    /// <summary>Displays the native trusted dialog.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(nint windowHandle, string text, string caption, uint type);

    /// <summary>Find a top-level window by its unique caption.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindow(string? className, string windowName);

    /// <summary>Post a close message to the owned prompt.</summary>
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint windowHandle, uint message, nint wParam, nint lParam);

    /// <summary>State captured by a cancellation callback.</summary>
    /// <param name="Caption">Unique window caption.</param>
    /// <param name="Completion">Dialog completion.</param>
    private sealed record DialogCancellationState(string Caption, Task Completion);
}

// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Automation;

/// <summary>FlaUI UIA3 discovery and attachment boundary.</summary>
public sealed class FlaUiAutomationBackend : IUiAutomationBackend
{
    private readonly IProcessIdentityProvider _processes;

    /// <summary>Initializes a new instance of the <see cref="FlaUiAutomationBackend"/> class.</summary>
    /// <param name="processes">Process identity resolver.</param>
    public FlaUiAutomationBackend(IProcessIdentityProvider processes) =>
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WindowDescriptor>> ListTopLevelWindowsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var automation = CreateAutomation();
        var elements = automation.GetDesktop()
            .FindAllChildren(static condition => condition.ByControlType(ControlType.Window));
        var windows = new List<WindowDescriptor>(elements.Length);

        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processId = UiaOperationGuard.Read(() => element.Properties.ProcessId.ValueOrDefault, 0);
            var identity = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
            var handle = UiaOperationGuard.Read(() => element.Properties.NativeWindowHandle.ValueOrDefault.ToInt64(), 0L);
            if (!IsDiscoverable(identity, handle))
            {
                continue;
            }

            windows.Add(
                new WindowDescriptor(
                    identity!,
                    handle,
                    UiaOperationGuard.ReadString(() => element.Name),
                    UiaOperationGuard.ReadString(() => element.ClassName),
                    UiaOperationGuard.ReadString(() => element.Properties.FrameworkId.ValueOrDefault),
                    UiaOperationGuard.Read(() => element.IsEnabled, false),
                    UiaOperationGuard.Read(() => element.IsOffscreen, true)));
        }

        return windows;
    }

    /// <inheritdoc/>
    public ValueTask<IUiAutomationSession> AttachAsync(
        ProcessIdentity target,
        long? windowHandle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var automation = CreateAutomation();
        try
        {
            var root = FindRoot(automation, target.ProcessId, windowHandle);
            var actualProcessId = root.Properties.ProcessId.Value;
            var handle = root.Properties.NativeWindowHandle.Value.ToInt64();
            ValidateAttachedWindow(actualProcessId, target.ProcessId, handle);

            IUiAutomationSession session = new FlaUiAutomationSession(automation, target, handle);
            return ValueTask.FromResult(session);
        }
        catch
        {
            automation.Dispose();
            throw;
        }
    }

    private static UIA3Automation CreateAutomation() =>
        new()
        {
            ConnectionTimeout = TimeSpan.FromSeconds(5),
            TransactionTimeout = TimeSpan.FromSeconds(5),
        };

    private static AutomationElement FindRoot(
        UIA3Automation automation,
        int processId,
        long? windowHandle)
    {
        if (windowHandle is long requestedHandle)
        {
            if (requestedHandle == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowHandle));
            }

            return automation.FromHandle(new IntPtr(requestedHandle));
        }

        return automation.GetDesktop()
                   .FindFirstChild(
                       condition => condition.ByControlType(ControlType.Window)
                           .And(condition.ByProcessId(processId)))
               ?? throw new InvalidOperationException("No top-level UI Automation window was found for the target process.");
    }

    internal static bool IsDiscoverable(ProcessIdentity? identity, long handle) =>
        identity is not null && handle != 0;

    internal static void ValidateAttachedWindow(
        int actualProcessId,
        int expectedProcessId,
        long handle)
    {
        if (actualProcessId != expectedProcessId)
        {
            throw new InvalidOperationException("The selected window is not owned by the consented process.");
        }

        if (handle == 0)
        {
            throw new InvalidOperationException("The selected UI Automation root has no native window handle.");
        }
    }
}

// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Windows.Automation;
using UIInspect.MCP.Windows.Processes;

namespace UIInspect.MCP.Tests;

/// <summary>Real UIA3 integration tests against deterministic WPF and WinForms target processes.</summary>
[NotInParallel]
public sealed class WindowsUiaIntegrationTests
{
    /// <summary>WPF exposes and executes every MVP UI Automation pattern.</summary>
    [Test]
    public async Task Wpf_fixture_supports_semantic_discovery_and_actions() =>
        await RunFixtureAsync("UIInspect.Sample.Wpf", "UIInspect WPF Sample", true);

    /// <summary>WinForms exposes the same public MVP surface through its UIA provider.</summary>
    [Test]
    public async Task WinForms_fixture_supports_semantic_discovery_and_actions() =>
        await RunFixtureAsync("UIInspect.Sample.WinForms", "UIInspect WinForms Sample", false);

    private static async Task RunFixtureAsync(
        string projectName,
        string expectedTitle,
        bool exerciseAllPatterns)
    {
        var executable = FindFixtureExecutable(projectName);
        using var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException($"Could not start {projectName}.");

        try
        {
            var handle = await WaitForMainWindowAsync(process);
            var processes = new WindowsProcessIdentityProvider();
            var identity = await processes.ResolveAsync(process.Id, CancellationToken.None)
                ?? throw new InvalidOperationException("Fixture identity was unavailable.");
            var backend = new FlaUiAutomationBackend(processes);
            var discovered = await WaitForDiscoveryAsync(backend, process.Id);
            await Assert.That(discovered.Title).IsEqualTo(expectedTitle);

            if (exerciseAllPatterns)
            {
                await ExerciseAttachmentGuardsAsync(backend, identity, handle);
            }

            await using var attached = await backend.AttachAsync(
                identity,
                exerciseAllPatterns ? null : handle.ToInt64(),
                CancellationToken.None);
            await Assert.That(attached.Target).IsEqualTo(identity);
            await Assert.That(attached.WindowHandle).IsEqualTo(handle.ToInt64());

            var snapshot = await attached.InspectAsync("integration", 12, 1000, CancellationToken.None);
            await Assert.That(snapshot.Nodes.Any(node => node.AutomationId == "InvokeButton")).IsTrue();
            await Assert.That(snapshot.Nodes.Any(node => node.AutomationId == "ValueTextBox")).IsTrue();
            var password = snapshot.Nodes.Single(node => node.AutomationId == "PasswordBox");
            await Assert.That(password.IsPassword).IsTrue();
            await Assert.That(password.Name).IsEqualTo("[redacted]");

            var truncatedByDepth = await attached.InspectAsync("integration", 0, 1000, CancellationToken.None);
            var truncatedByNodes = await attached.InspectAsync("integration", 12, 1, CancellationToken.None);
            await Assert.That(truncatedByDepth.Truncated).IsTrue();
            await Assert.That(truncatedByNodes.Truncated).IsTrue();

            if (exerciseAllPatterns)
            {
                var rootReference = (await SnapshotAsync(attached)).Nodes
                    .Single(node => node.ParentReference is null)
                    .ElementReference;
                var unsupportedRootValue = await attached.SetValueAsync(
                    rootReference,
                    "not-supported",
                    CancellationToken.None);
                await Assert.That(unsupportedRootValue.ErrorCode).IsEqualTo("pattern_not_supported");

                var transientReference = FindReference(await SnapshotAsync(attached), "TransientTarget");
                InvokeExternally(handle, "RemoveTransientButton");
                var disappeared = await attached.InvokeAsync(transientReference, CancellationToken.None);
                await Assert.That(disappeared.ErrorCode).IsEqualTo("stale_element");
            }

            var invokeReference = FindReference(await SnapshotAsync(attached), "InvokeButton");
            var invoke = await attached.InvokeAsync(invokeReference, CancellationToken.None);
            await Assert.That(invoke.Succeeded).IsTrue();
            await Assert.That(await WaitForValueAsync(handle, "ResultText", "invoked:1")).IsTrue();
            var stale = await attached.InvokeAsync(invokeReference, CancellationToken.None);
            await Assert.That(stale.ErrorCode).IsEqualTo("stale_element");

            var clickReference = FindReference(await SnapshotAsync(attached), "InvokeButton");
            var click = await attached.ClickAsync(clickReference, CancellationToken.None);
            await Assert.That(click.Succeeded).IsTrue();
            await Assert.That(await WaitForValueAsync(handle, "ResultText", "invoked:2")).IsTrue();

            var valueReference = FindReference(await SnapshotAsync(attached), "ValueTextBox");
            var setValue = await attached.SetValueAsync(valueReference, "changed", CancellationToken.None);
            await Assert.That(setValue.Succeeded).IsTrue();
            await Assert.That(await WaitForValueAsync(handle, "ResultText", "value:changed")).IsTrue();

            var unsupported = await attached.InvokeAsync(
                FindReference(await SnapshotAsync(attached), "ValueTextBox"),
                CancellationToken.None);
            await Assert.That(unsupported.ErrorCode).IsEqualTo("pattern_not_supported");
            var disabled = await attached.InvokeAsync(
                FindReference(await SnapshotAsync(attached), "DisabledButton"),
                CancellationToken.None);
            await Assert.That(disabled.ErrorCode).IsEqualTo("element_disabled");
            var keyNotAllowed = await attached.SendKeyAsync("missing", "WINDOWS", CancellationToken.None);
            await Assert.That(keyNotAllowed.ErrorCode).IsEqualTo("key_not_allowed");

            if (exerciseAllPatterns)
            {
                await ExerciseWpfPatternsAsync(attached, handle);
            }

            await attached.DisposeAsync();
            await attached.DisposeAsync();
            await Assert.That(async () => { _ = await attached.InspectAsync("disposed", 1, 1, CancellationToken.None); }).Throws<ObjectDisposedException>();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static async Task ExerciseWpfPatternsAsync(
        UIInspect.MCP.Core.Abstractions.IUiAutomationSession attached,
        IntPtr handle)
    {
        var comboReference = FindReference(await SnapshotAsync(attached), "ColorComboBox");
        var expandCombo = await attached.SetExpandedAsync(comboReference, true, CancellationToken.None);
        await Assert.That(expandCombo.Succeeded).IsTrue();

        var expandedSnapshot = await SnapshotAsync(attached);
        var greenReference = expandedSnapshot.Nodes
            .First(node =>
                node.AutomationId == "GreenItem" &&
                node.SupportedPatterns.Contains("SelectionItem", StringComparer.Ordinal))
            .ElementReference;
        var select = await attached.SelectAsync(greenReference, CancellationToken.None);
        await Assert.That(select.Succeeded).IsTrue();
        await Assert.That(await WaitForValueAsync(handle, "ResultText", "selection:Green")).IsTrue();

        var childReference = FindReference(await SnapshotAsync(attached), "ChildNode");
        var expandTree = await attached.SetExpandedAsync(childReference, true, CancellationToken.None);
        await Assert.That(expandTree.Succeeded).IsTrue();
        await Assert.That(await WaitForValueAsync(handle, "ResultText", "expand:Child")).IsTrue();

        childReference = FindReference(await SnapshotAsync(attached), "ChildNode");
        var collapseTree = await attached.SetExpandedAsync(childReference, false, CancellationToken.None);
        await Assert.That(collapseTree.Succeeded).IsTrue();
        await Assert.That(await WaitForValueAsync(handle, "ResultText", "collapse:Child")).IsTrue();

        var keyboardReference = FindReference(await SnapshotAsync(attached), "KeyboardTextBox");
        var key = await attached.SendKeyAsync(keyboardReference, "ENTER", CancellationToken.None);
        await Assert.That(key.Succeeded).IsTrue();
        await Assert.That(await WaitForValueAsync(handle, "ResultText", "key:Return")).IsTrue();

        var readOnlyReference = FindReference(await SnapshotAsync(attached), "ResultText");
        var readOnly = await attached.SetValueAsync(readOnlyReference, "forbidden", CancellationToken.None);
        await Assert.That(readOnly.ErrorCode).IsEqualTo("read_only");
        var unsupportedSelect = await attached.SelectAsync(
            FindReference(await SnapshotAsync(attached), "InvokeButton"),
            CancellationToken.None);
        await Assert.That(unsupportedSelect.ErrorCode).IsEqualTo("pattern_not_supported");
        var unsupportedExpand = await attached.SetExpandedAsync(
            FindReference(await SnapshotAsync(attached), "InvokeButton"),
            true,
            CancellationToken.None);
        await Assert.That(unsupportedExpand.ErrorCode).IsEqualTo("pattern_not_supported");
    }

    private static async Task ExerciseAttachmentGuardsAsync(
        FlaUiAutomationBackend backend,
        ProcessIdentity identity,
        IntPtr handle)
    {
        var wrongIdentity = identity with { ProcessId = int.MaxValue };
        await Assert.That(
            async () => { _ = await backend.AttachAsync(identity, 0, CancellationToken.None); })
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(
            async () => { _ = await backend.AttachAsync(wrongIdentity, handle.ToInt64(), CancellationToken.None); })
            .Throws<InvalidOperationException>();
        await Assert.That(
            async () => { _ = await backend.AttachAsync(wrongIdentity, null, CancellationToken.None); })
            .Throws<InvalidOperationException>();

        await Assert.That(() => new FlaUiAutomationSession(null!, identity, handle.ToInt64()))
            .Throws<ArgumentNullException>();
        using (var automation = new UIA3Automation())
        {
            await Assert.That(() => new FlaUiAutomationSession(automation, null!, handle.ToInt64()))
                .Throws<ArgumentNullException>();
        }

        using (var automation = new UIA3Automation())
        {
            await Assert.That(() => new FlaUiAutomationSession(automation, identity, 0))
                .Throws<ArgumentOutOfRangeException>();
        }

        var mismatchedAutomation = new UIA3Automation();
        await using var mismatchedSession = new FlaUiAutomationSession(
            mismatchedAutomation,
            wrongIdentity,
            handle.ToInt64());
        await Assert.That(
            async () =>
            {
                _ = await mismatchedSession.InspectAsync(
                    "mismatched",
                    1,
                    10,
                    CancellationToken.None);
            })
            .Throws<InvalidOperationException>();
    }

    private static ValueTask<UiTreeSnapshot> SnapshotAsync(
        UIInspect.MCP.Core.Abstractions.IUiAutomationSession session) =>
        session.InspectAsync("integration", 12, 1000, CancellationToken.None);

    private static string FindReference(UiTreeSnapshot snapshot, string automationId) =>
        snapshot.Nodes.First(node => node.AutomationId == automationId).ElementReference;

    private static string FindFixtureExecutable(string projectName)
    {
        var root = FindWorkspaceRoot();
        var executable = Path.Combine(
            root,
            "src",
            projectName,
            "bin",
            "Release",
            "net10.0-windows",
            $"{projectName}.exe");
        return File.Exists(executable)
            ? executable
            : throw new FileNotFoundException($"Build fixture {projectName} before running integration tests.", executable);
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

    private static async Task<IntPtr> WaitForMainWindowAsync(Process process)
    {
        var timeout = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < timeout)
        {
            process.Refresh();
            if (process.HasExited)
            {
                throw new InvalidOperationException("Fixture process exited before creating its window.");
            }

            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process.MainWindowHandle;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Fixture did not create a main window.");
    }

    private static async Task<WindowDescriptor> WaitForDiscoveryAsync(
        FlaUiAutomationBackend backend,
        int processId)
    {
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < timeout)
        {
            var windows = await backend.ListTopLevelWindowsAsync(CancellationToken.None);
            var found = windows.FirstOrDefault(window => window.Process.ProcessId == processId);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Fixture was not discovered through UIA3.");
    }

    private static async Task<bool> WaitForValueAsync(
        IntPtr handle,
        string automationId,
        string expected)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeout)
        {
            using var automation = new UIA3Automation();
            var root = automation.FromHandle(handle);
            var element = root.FindFirstDescendant(
                condition => condition.ByAutomationId(automationId));
            var value = element?.Patterns.Value.PatternOrDefault?.Value.Value;
            if (string.Equals(value, expected, StringComparison.Ordinal))
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    private static void InvokeExternally(IntPtr handle, string automationId)
    {
        using var automation = new UIA3Automation();
        var root = automation.FromHandle(handle);
        var element = root.FindFirstDescendant(
            condition => condition.ByAutomationId(automationId));
        element!.Patterns.Invoke.Pattern.Invoke();
    }
}

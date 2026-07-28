// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Windows.Automation;
using UIInspect.MCP.Windows.Processes;

namespace UIInspect.MCP.Tests;

/// <summary>Real UIA3 integration tests against deterministic WPF and WinForms target processes.</summary>
[NotInParallel]
public sealed class WindowsUiaIntegrationTests
{
    /// <summary>The full inspection depth.</summary>
    private const int InspectionDepth = 12;

    /// <summary>The full inspection node limit.</summary>
    private const int InspectionNodeLimit = 1_000;

    /// <summary>The minimal inspection node limit.</summary>
    private const int MinimalInspectionLimit = 1;

    /// <summary>The compact node limit used by invalid-session validation.</summary>
    private const int CompactInspectionNodeLimit = 10;

    /// <summary>The integration session identifier.</summary>
    private const string InspectionSessionId = "integration";

    /// <summary>The WPF sample project name.</summary>
    private const string WpfProjectName = "UIInspect.Sample.Wpf";

    /// <summary>The WinForms sample project name.</summary>
    private const string WinFormsProjectName = "UIInspect.Sample.WinForms";

    /// <summary>The WPF sample title.</summary>
    private const string WpfTitle = "UIInspect WPF Sample";

    /// <summary>The WinForms sample title.</summary>
    private const string WinFormsTitle = "UIInspect WinForms Sample";

    /// <summary>The result text automation ID.</summary>
    private const string ResultTextId = "ResultText";

    /// <summary>The invoke button automation ID.</summary>
    private const string InvokeButtonId = "InvokeButton";

    /// <summary>The editable text box automation ID.</summary>
    private const string ValueTextBoxId = "ValueTextBox";

    /// <summary>The password box automation ID.</summary>
    private const string PasswordBoxId = "PasswordBox";

    /// <summary>The disabled button automation ID.</summary>
    private const string DisabledButtonId = "DisabledButton";

    /// <summary>The transient target automation ID.</summary>
    private const string TransientTargetId = "TransientTarget";

    /// <summary>The transient-control removal button automation ID.</summary>
    private const string RemoveTransientButtonId = "RemoveTransientButton";

    /// <summary>The color combo box automation ID.</summary>
    private const string ColorComboBoxId = "ColorComboBox";

    /// <summary>The green option automation ID.</summary>
    private const string GreenItemId = "GreenItem";

    /// <summary>The tree child automation ID.</summary>
    private const string ChildNodeId = "ChildNode";

    /// <summary>The keyboard text box automation ID.</summary>
    private const string KeyboardTextBoxId = "KeyboardTextBox";

    /// <summary>The polling cadence in milliseconds.</summary>
    private const int PollingIntervalMilliseconds = 50;

    /// <summary>The expected normalized unsupported-pattern code.</summary>
    private const string PatternNotSupported = "pattern_not_supported";

    /// <summary>The fixture main-window startup timeout.</summary>
    private static readonly TimeSpan FixtureStartupTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The UIA discovery timeout.</summary>
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The result propagation timeout.</summary>
    private static readonly TimeSpan ValueTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The interval used by bounded polling.</summary>
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(PollingIntervalMilliseconds);

    /// <summary>WPF exposes and executes every MVP UI Automation pattern.</summary>
    /// <returns>A task representing the fixture assertions.</returns>
    [Test]
    public Task Wpf_fixture_supports_semantic_discovery_and_actions() =>
        RunFixtureAsync(WpfProjectName, WpfTitle, true, TimeProvider.System);

    /// <summary>WinForms exposes the cross-framework discovery, invocation, and value MVP surface.</summary>
    /// <returns>A task representing the fixture assertions.</returns>
    [Test]
    public Task WinForms_fixture_supports_semantic_discovery_and_actions() =>
        RunFixtureAsync(WinFormsProjectName, WinFormsTitle, false, TimeProvider.System);

    /// <summary>Starts one fixture, then validates its discoverable UIA surface.</summary>
    /// <param name="projectName">The sample project assembly name.</param>
    /// <param name="expectedTitle">The expected top-level window title.</param>
    /// <param name="exerciseWpfPatterns">Whether WPF-specific fixture controls are present.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>A task representing the complete fixture exercise.</returns>
    private static async Task RunFixtureAsync(
        string projectName,
        string expectedTitle,
        bool exerciseWpfPatterns,
        TimeProvider timeProvider)
    {
        using var process = StartFixture(projectName);
        try
        {
            var handle = await WaitForMainWindowAsync(process, timeProvider);
            var backend = new FlaUiAutomationBackend(new WindowsProcessIdentityProvider());
            var identity = await ResolveFixtureIdentityAsync(process, backend, expectedTitle, timeProvider);
            await ExerciseFixtureAsync(backend, identity, handle, exerciseWpfPatterns, timeProvider);
        }
        finally
        {
            await StopFixtureAsync(process);
        }
    }

    /// <summary>Creates a semantic UIA attachment and runs all framework-neutral actions.</summary>
    /// <param name="backend">The UIA backend under test.</param>
    /// <param name="identity">The discovered target process.</param>
    /// <param name="handle">The target top-level window handle.</param>
    /// <param name="exerciseWpfPatterns">Whether WPF-specific controls are available.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>A task representing the attachment exercise.</returns>
    private static async Task ExerciseFixtureAsync(
        FlaUiAutomationBackend backend,
        ProcessIdentity identity,
        IntPtr handle,
        bool exerciseWpfPatterns,
        TimeProvider timeProvider)
    {
        if (exerciseWpfPatterns)
        {
            await ExerciseAttachmentGuardsAsync(backend, identity, handle);
        }

        await using var attached = await backend.AttachAsync(
            identity,
            exerciseWpfPatterns ? null : handle.ToInt64(),
            CancellationToken.None);
        await Assert.That(attached.Target).IsEqualTo(identity);
        await Assert.That(attached.WindowHandle).IsEqualTo(handle.ToInt64());
        await AssertSemanticTreeAsync(attached);

        if (exerciseWpfPatterns)
        {
            await ExerciseStaleElementAsync(attached, handle);
        }

        await ExerciseFrameworkNeutralActionsAsync(attached, handle, timeProvider);

        if (exerciseWpfPatterns)
        {
            await ExerciseWpfOnlyActionsAsync(attached, handle, timeProvider);
        }

        await AssertDisposedSessionAsync(attached);
    }

    /// <summary>Validates that semantic inspection retains IDs and protects password text.</summary>
    /// <param name="attached">The active UIA attachment.</param>
    /// <returns>A task representing the assertions.</returns>
    private static async Task AssertSemanticTreeAsync(IUiAutomationSession attached)
    {
        var snapshot = await SnapshotAsync(attached);
        await Assert.That(ContainsAutomationId(snapshot, InvokeButtonId)).IsTrue();
        await Assert.That(ContainsAutomationId(snapshot, ValueTextBoxId)).IsTrue();
        var password = FindNode(snapshot, PasswordBoxId);
        await Assert.That(password.IsPassword).IsTrue();
        await Assert.That(password.Name).IsEqualTo("[redacted]");

        var truncatedByDepth = await attached.InspectAsync(
            InspectionSessionId,
            0,
            InspectionNodeLimit,
            CancellationToken.None);
        var truncatedByNodes = await attached.InspectAsync(
            InspectionSessionId,
            InspectionDepth,
            MinimalInspectionLimit,
            CancellationToken.None);
        await Assert.That(truncatedByDepth.Truncated).IsTrue();
        await Assert.That(truncatedByNodes.Truncated).IsTrue();
    }

    /// <summary>Exercises stale-element protection after the fixture removes a referenced control.</summary>
    /// <param name="attached">The active UIA attachment.</param>
    /// <param name="handle">The target top-level window handle.</param>
    /// <returns>A task representing the stale-element assertion.</returns>
    private static async Task ExerciseStaleElementAsync(IUiAutomationSession attached, IntPtr handle)
    {
        var rootReference = FindRootReference(await SnapshotAsync(attached));
        var unsupported = await attached.SetValueAsync(rootReference, "not-supported", CancellationToken.None);
        await Assert.That(unsupported.ErrorCode).IsEqualTo(PatternNotSupported);
        var transientReference = FindReference(await SnapshotAsync(attached), TransientTargetId);
        InvokeExternally(handle, RemoveTransientButtonId);
        var disappeared = await attached.InvokeAsync(transientReference, CancellationToken.None);
        await Assert.That(disappeared.ErrorCode).IsEqualTo("stale_element");
    }

    /// <summary>Exercises actions available from both WPF and WinForms UIA providers.</summary>
    /// <param name="attached">The active UIA attachment.</param>
    /// <param name="handle">The target top-level window handle.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>A task representing the action exercise.</returns>
    private static async Task ExerciseFrameworkNeutralActionsAsync(
        IUiAutomationSession attached,
        IntPtr handle,
        TimeProvider timeProvider)
    {
        var invokeReference = FindReference(await SnapshotAsync(attached), InvokeButtonId);
        var invoke = await attached.InvokeAsync(invokeReference, CancellationToken.None);
        await Assert.That(invoke.Succeeded).IsTrue();
        await AssertResultAsync(handle, "invoked:1", timeProvider);
        var stale = await attached.InvokeAsync(invokeReference, CancellationToken.None);
        await Assert.That(stale.ErrorCode).IsEqualTo("stale_element");

        var clickReference = FindReference(await SnapshotAsync(attached), InvokeButtonId);
        var click = await attached.ClickAsync(clickReference, CancellationToken.None);
        await Assert.That(click.Succeeded).IsTrue();
        await AssertResultAsync(handle, "invoked:2", timeProvider);

        var valueReference = FindReference(await SnapshotAsync(attached), ValueTextBoxId);
        var setValue = await attached.SetValueAsync(valueReference, "changed", CancellationToken.None);
        await Assert.That(setValue.Succeeded).IsTrue();
        await AssertResultAsync(handle, "value:changed", timeProvider);

        await AssertUnsupportedNeutralActionsAsync(attached);
    }

    /// <summary>Validates deterministic errors from invalid generic actions.</summary>
    /// <param name="attached">The active UIA attachment.</param>
    /// <returns>A task representing the assertions.</returns>
    private static async Task AssertUnsupportedNeutralActionsAsync(IUiAutomationSession attached)
    {
        var unsupported = await attached.InvokeAsync(
            FindReference(await SnapshotAsync(attached), ValueTextBoxId),
            CancellationToken.None);
        await Assert.That(unsupported.ErrorCode).IsEqualTo(PatternNotSupported);
        var disabled = await attached.InvokeAsync(
            FindReference(await SnapshotAsync(attached), DisabledButtonId),
            CancellationToken.None);
        await Assert.That(disabled.ErrorCode).IsEqualTo("element_disabled");
        var keyNotAllowed = await attached.SendKeyAsync("missing", "WINDOWS", CancellationToken.None);
        await Assert.That(keyNotAllowed.ErrorCode).IsEqualTo("key_not_allowed");
    }

    /// <summary>Exercises WPF controls that deliberately cover selection, expansion, and logical keys.</summary>
    /// <param name="attached">The active UIA attachment.</param>
    /// <param name="handle">The target top-level window handle.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>A task representing the WPF-specific action exercise.</returns>
    private static async Task ExerciseWpfOnlyActionsAsync(
        IUiAutomationSession attached,
        IntPtr handle,
        TimeProvider timeProvider)
    {
        var comboReference = FindReference(await SnapshotAsync(attached), ColorComboBoxId);
        await Assert.That((await attached.SetExpandedAsync(comboReference, true, CancellationToken.None)).Succeeded).IsTrue();
        var greenReference = FindSelectionItemReference(await SnapshotAsync(attached), GreenItemId);
        await Assert.That((await attached.SelectAsync(greenReference, CancellationToken.None)).Succeeded).IsTrue();
        await AssertResultAsync(handle, "selection:Green", timeProvider);

        var childReference = FindReference(await SnapshotAsync(attached), ChildNodeId);
        await Assert.That((await attached.SetExpandedAsync(childReference, true, CancellationToken.None)).Succeeded).IsTrue();
        await AssertResultAsync(handle, "expand:Child", timeProvider);
        childReference = FindReference(await SnapshotAsync(attached), ChildNodeId);
        await Assert.That((await attached.SetExpandedAsync(childReference, false, CancellationToken.None)).Succeeded).IsTrue();
        await AssertResultAsync(handle, "collapse:Child", timeProvider);

        var keyboardReference = FindReference(await SnapshotAsync(attached), KeyboardTextBoxId);
        await Assert.That((await attached.SendKeyAsync(keyboardReference, "ENTER", CancellationToken.None)).Succeeded).IsTrue();
        await AssertResultAsync(handle, "key:Return", timeProvider);
        await AssertWpfUnsupportedActionsAsync(attached);
    }

    /// <summary>Validates WPF read-only and unsupported-pattern results.</summary>
    /// <param name="attached">The active UIA attachment.</param>
    /// <returns>A task representing the assertions.</returns>
    private static async Task AssertWpfUnsupportedActionsAsync(IUiAutomationSession attached)
    {
        var readOnly = await attached.SetValueAsync(
            FindReference(await SnapshotAsync(attached), ResultTextId),
            "forbidden",
            CancellationToken.None);
        await Assert.That(readOnly.ErrorCode).IsEqualTo("read_only");
        var unsupportedSelect = await attached.SelectAsync(
            FindReference(await SnapshotAsync(attached), InvokeButtonId),
            CancellationToken.None);
        await Assert.That(unsupportedSelect.ErrorCode).IsEqualTo(PatternNotSupported);
        var unsupportedExpand = await attached.SetExpandedAsync(
            FindReference(await SnapshotAsync(attached), InvokeButtonId),
            true,
            CancellationToken.None);
        await Assert.That(unsupportedExpand.ErrorCode).IsEqualTo(PatternNotSupported);
    }

    /// <summary>Exercises attachment constructor and target identity invariants.</summary>
    /// <param name="backend">The UIA backend under test.</param>
    /// <param name="identity">The real fixture process identity.</param>
    /// <param name="handle">The fixture top-level window handle.</param>
    /// <returns>A task representing the guard assertions.</returns>
    private static async Task ExerciseAttachmentGuardsAsync(
        FlaUiAutomationBackend backend,
        ProcessIdentity identity,
        IntPtr handle)
    {
        var wrongIdentity = identity with { ProcessId = int.MaxValue };
        await Assert.That(async () => { _ = await backend.AttachAsync(identity, 0, CancellationToken.None); })
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(async () => { _ = await backend.AttachAsync(wrongIdentity, handle.ToInt64(), CancellationToken.None); })
            .Throws<InvalidOperationException>();
        await Assert.That(async () => { _ = await backend.AttachAsync(wrongIdentity, null, CancellationToken.None); })
            .Throws<InvalidOperationException>();
        await Assert.That(() => new FlaUiAutomationSession(null!, identity, handle.ToInt64()))
            .Throws<ArgumentNullException>();
        await AssertInvalidSessionArgumentsAsync(identity, handle);
        await AssertMismatchedSessionFailsAsync(wrongIdentity, handle);
    }

    /// <summary>Verifies immediate constructor validation for invalid session inputs.</summary>
    /// <param name="identity">The valid identity used to isolate invalid arguments.</param>
    /// <param name="handle">The valid window handle used to isolate invalid arguments.</param>
    /// <returns>A task representing the constructor assertions.</returns>
    private static async Task AssertInvalidSessionArgumentsAsync(ProcessIdentity identity, IntPtr handle)
    {
        using var automation = new UIA3Automation();
        await Assert.That(() => new FlaUiAutomationSession(automation, null!, handle.ToInt64()))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new FlaUiAutomationSession(automation, identity, 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Verifies target process validation for a session constructed with another identity.</summary>
    /// <param name="wrongIdentity">An identity that cannot own the target handle.</param>
    /// <param name="handle">The fixture top-level window handle.</param>
    /// <returns>A task representing the mismatch assertion.</returns>
    private static async Task AssertMismatchedSessionFailsAsync(ProcessIdentity wrongIdentity, IntPtr handle)
    {
        var mismatchedAutomation = new UIA3Automation();
        await using var session = new FlaUiAutomationSession(mismatchedAutomation, wrongIdentity, handle.ToInt64());
        await Assert.That(async () =>
        {
            _ = await session.InspectAsync(
                InspectionSessionId,
                MinimalInspectionLimit,
                CompactInspectionNodeLimit,
                CancellationToken.None);
        }).Throws<InvalidOperationException>();
    }

    /// <summary>Disposes an attachment twice and confirms subsequent operations fail deterministically.</summary>
    /// <param name="attached">The attachment to dispose.</param>
    /// <returns>A task representing the disposal assertions.</returns>
    private static async Task AssertDisposedSessionAsync(IUiAutomationSession attached)
    {
        await attached.DisposeAsync();
        await attached.DisposeAsync();
        await Assert.That(async () =>
        {
            _ = await attached.InspectAsync("disposed", MinimalInspectionLimit, MinimalInspectionLimit, CancellationToken.None);
        }).Throws<ObjectDisposedException>();
    }

    /// <summary>Captures a full integration snapshot.</summary>
    /// <param name="session">The session to inspect.</param>
    /// <returns>The semantic UI tree.</returns>
    private static ValueTask<UiTreeSnapshot> SnapshotAsync(IUiAutomationSession session) =>
        session.InspectAsync(InspectionSessionId, InspectionDepth, InspectionNodeLimit, CancellationToken.None);

    /// <summary>Finds one node by its automation ID.</summary>
    /// <param name="snapshot">The snapshot to search.</param>
    /// <param name="automationId">The expected automation ID.</param>
    /// <returns>The matching node.</returns>
    private static UiElementNode FindNode(UiTreeSnapshot snapshot, string automationId)
    {
        foreach (var node in snapshot.Nodes)
        {
            if (string.Equals(node.AutomationId, automationId, StringComparison.Ordinal))
            {
                return node;
            }
        }

        throw new InvalidOperationException($"Fixture node '{automationId}' was not found.");
    }

    /// <summary>Gets a stable element reference for one fixture node.</summary>
    /// <param name="snapshot">The snapshot to search.</param>
    /// <param name="automationId">The expected automation ID.</param>
    /// <returns>The matching stable element reference.</returns>
    private static string FindReference(UiTreeSnapshot snapshot, string automationId) =>
        FindNode(snapshot, automationId).ElementReference;

    /// <summary>Gets the stable reference for the snapshot root.</summary>
    /// <param name="snapshot">The snapshot to search.</param>
    /// <returns>The root stable element reference.</returns>
    private static string FindRootReference(UiTreeSnapshot snapshot)
    {
        foreach (var node in snapshot.Nodes)
        {
            if (node.ParentReference is null)
            {
                return node.ElementReference;
            }
        }

        throw new InvalidOperationException("Fixture root node was not found.");
    }

    /// <summary>Gets a stable reference for the expected selection item.</summary>
    /// <param name="snapshot">The snapshot to search.</param>
    /// <param name="automationId">The expected automation ID.</param>
    /// <returns>The matching selection-item reference.</returns>
    private static string FindSelectionItemReference(UiTreeSnapshot snapshot, string automationId)
    {
        foreach (var node in snapshot.Nodes)
        {
            if (string.Equals(node.AutomationId, automationId, StringComparison.Ordinal)
                && SupportsPattern(node, "SelectionItem"))
            {
                return node.ElementReference;
            }
        }

        throw new InvalidOperationException($"Fixture selection item '{automationId}' was not found.");
    }

    /// <summary>Determines whether a node reports a named UIA pattern.</summary>
    /// <param name="node">The semantic node to inspect.</param>
    /// <param name="pattern">The expected pattern name.</param>
    /// <returns><see langword="true"/> when the pattern is supported.</returns>
    private static bool SupportsPattern(UiElementNode node, string pattern)
    {
        foreach (var supportedPattern in node.SupportedPatterns)
        {
            if (string.Equals(supportedPattern, pattern, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a snapshot contains one automation ID.</summary>
    /// <param name="snapshot">The snapshot to search.</param>
    /// <param name="automationId">The automation ID to find.</param>
    /// <returns><see langword="true"/> when the node exists; otherwise <see langword="false"/>.</returns>
    private static bool ContainsAutomationId(UiTreeSnapshot snapshot, string automationId)
    {
        foreach (var node in snapshot.Nodes)
        {
            if (string.Equals(node.AutomationId, automationId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Starts the compiled sample fixture process.</summary>
    /// <param name="projectName">The sample project assembly name.</param>
    /// <returns>The started fixture process.</returns>
    private static Process StartFixture(string projectName) => Process.Start(new ProcessStartInfo { FileName = FindFixtureExecutable(projectName), UseShellExecute = false })
        ?? throw new InvalidOperationException($"Could not start {projectName}.");

    /// <summary>Stops a sample fixture after its UIA assertions complete.</summary>
    /// <param name="process">The fixture process to stop.</param>
    /// <returns>A task representing process cleanup.</returns>
    private static async Task StopFixtureAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(true);
            await process.WaitForExitAsync();
        }
    }

    /// <summary>Resolves the sample process identity after UIA discovery confirms its window.</summary>
    /// <param name="process">The fixture process.</param>
    /// <param name="backend">The UIA backend used for discovery.</param>
    /// <param name="expectedTitle">The expected window title.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>The process identity used for attachment.</returns>
    private static async Task<ProcessIdentity> ResolveFixtureIdentityAsync(
        Process process,
        FlaUiAutomationBackend backend,
        string expectedTitle,
        TimeProvider timeProvider)
    {
        var processes = new WindowsProcessIdentityProvider();
        var identity = await processes.ResolveAsync(process.Id, CancellationToken.None)
            ?? throw new InvalidOperationException("Fixture identity was unavailable.");
        var discovered = await WaitForDiscoveryAsync(backend, process.Id, timeProvider);
        await Assert.That(discovered.Title).IsEqualTo(expectedTitle);
        return identity;
    }

    /// <summary>Locates a compiled sample executable from the workspace root.</summary>
    /// <param name="projectName">The sample project assembly name.</param>
    /// <returns>The compiled executable path.</returns>
    private static string FindFixtureExecutable(string projectName)
    {
        var executable = Path.Combine(
            FindWorkspaceRoot(),
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

    /// <summary>Finds the repository root from the test output directory.</summary>
    /// <returns>The repository root path.</returns>
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

    /// <summary>Waits for the fixture's native top-level window.</summary>
    /// <param name="process">The started fixture process.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>The main window handle.</returns>
    private static async Task<IntPtr> WaitForMainWindowAsync(Process process, TimeProvider timeProvider)
    {
        var timestamp = timeProvider.GetTimestamp();
        using var timer = new PeriodicTimer(PollingInterval, timeProvider);
        while (timeProvider.GetElapsedTime(timestamp) < FixtureStartupTimeout)
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

            _ = await timer.WaitForNextTickAsync();
        }

        throw new TimeoutException("Fixture did not create a main window.");
    }

    /// <summary>Waits for UIA3 to enumerate the fixture top-level window.</summary>
    /// <param name="backend">The UIA backend under test.</param>
    /// <param name="processId">The fixture process ID.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>The discovered window descriptor.</returns>
    private static async Task<WindowDescriptor> WaitForDiscoveryAsync(
        FlaUiAutomationBackend backend,
        int processId,
        TimeProvider timeProvider)
    {
        var timestamp = timeProvider.GetTimestamp();
        using var timer = new PeriodicTimer(PollingInterval, timeProvider);
        while (timeProvider.GetElapsedTime(timestamp) < DiscoveryTimeout)
        {
            var windows = await backend.ListTopLevelWindowsAsync(CancellationToken.None);
            foreach (var window in windows)
            {
                if (window.Process.ProcessId == processId)
                {
                    return window;
                }
            }

            _ = await timer.WaitForNextTickAsync();
        }

        throw new TimeoutException("Fixture was not discovered through UIA3.");
    }

    /// <summary>Waits for the fixture to publish an expected result string.</summary>
    /// <param name="handle">The fixture top-level window handle.</param>
    /// <param name="expected">The expected result text.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns>A task representing the value assertion.</returns>
    private static async Task AssertResultAsync(IntPtr handle, string expected, TimeProvider timeProvider) =>
        await Assert.That(await WaitForValueAsync(handle, ResultTextId, expected, timeProvider)).IsTrue();

    /// <summary>Waits for a UIA value pattern to equal an expected string.</summary>
    /// <param name="handle">The fixture top-level window handle.</param>
    /// <param name="automationId">The element to read.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="timeProvider">The clock used by bounded polling.</param>
    /// <returns><see langword="true"/> when the expected value appears before timeout.</returns>
    private static async Task<bool> WaitForValueAsync(
        IntPtr handle,
        string automationId,
        string expected,
        TimeProvider timeProvider)
    {
        var timestamp = timeProvider.GetTimestamp();
        using var timer = new PeriodicTimer(PollingInterval, timeProvider);
        while (timeProvider.GetElapsedTime(timestamp) < ValueTimeout)
        {
            using var automation = new UIA3Automation();
            var root = automation.FromHandle(handle);
            var element = root.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
            var value = element?.Patterns.Value.PatternOrDefault?.Value.Value;
            if (string.Equals(value, expected, StringComparison.Ordinal))
            {
                return true;
            }

            _ = await timer.WaitForNextTickAsync();
        }

        return false;
    }

    /// <summary>Invokes a fixture control outside the attachment to deliberately make an element stale.</summary>
    /// <param name="handle">The fixture top-level window handle.</param>
    /// <param name="automationId">The control to invoke.</param>
    private static void InvokeExternally(IntPtr handle, string automationId)
    {
        using var automation = new UIA3Automation();
        var root = automation.FromHandle(handle);
        var element = root.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
        element!.Patterns.Invoke.Pattern.Invoke();
    }
}

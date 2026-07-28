// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlaUI.Core.Exceptions;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Windows.Automation;
using UIInspect.MCP.Windows.Processes;

namespace UIInspect.MCP.Tests;

/// <summary>Exercises provider-independent Windows adapter decisions and exception translations.</summary>
public sealed class WindowsAdapterUnitTests
{
    /// <summary>The deterministic test process ID.</summary>
    private const int ProcessId = 1;

    /// <summary>The deterministic test window handle.</summary>
    private const long WindowHandle = 1;

    /// <summary>The deterministic test process name.</summary>
    private const string ProcessName = "app";

    /// <summary>The deterministic test executable name.</summary>
    private const string ExecutableName = "app.exe";

    /// <summary>The expected normalized provider failure code.</summary>
    private const string AutomationFailed = "automation_failed";

    /// <summary>The successful guard read value.</summary>
    private const int SuccessfulRead = 7;

    /// <summary>The first fallback read value.</summary>
    private const int InvalidOperationFallback = -1;

    /// <summary>The second fallback read value.</summary>
    private const int ComFallback = -2;

    /// <summary>The third fallback read value.</summary>
    private const int UnsupportedFallback = -3;

    /// <summary>The mismatching process ID.</summary>
    private const int OtherProcessId = 2;

    /// <summary>Expected UIA read and action exceptions become deterministic safe fallbacks.</summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Uia_operation_guard_translates_expected_provider_failures()
    {
        var unsupported = (PropertyNotSupportedException)RuntimeHelpers.GetUninitializedObject(
            typeof(PropertyNotSupportedException));

        await Assert.That(UiaOperationGuard.Read(static () => SuccessfulRead, InvalidOperationFallback)).IsEqualTo(SuccessfulRead);
        await Assert.That(UiaOperationGuard.Read(static () => throw new InvalidOperationException(), InvalidOperationFallback)).IsEqualTo(InvalidOperationFallback);
        await Assert.That(UiaOperationGuard.Read(static () => throw new TestComException(), ComFallback)).IsEqualTo(ComFallback);
        await Assert.That(UiaOperationGuard.Read(() => throw unsupported, UnsupportedFallback)).IsEqualTo(UnsupportedFallback);
        await Assert.That(UiaOperationGuard.ReadString(static () => "value")).IsEqualTo("value");
        await Assert.That(UiaOperationGuard.ReadString(static () => null)).IsEqualTo(string.Empty);
        await Assert.That(UiaOperationGuard.Execute(static () => PlatformActionResult.Ok("ok")).Succeeded).IsTrue();
        await Assert.That(UiaOperationGuard.Execute(static () => throw new InvalidOperationException()).ErrorCode).IsEqualTo(AutomationFailed);
        await Assert.That(UiaOperationGuard.Execute(static () => throw new TestComException()).ErrorCode).IsEqualTo(AutomationFailed);
        await Assert.That(UiaOperationGuard.Execute(static () => throw new ArgumentException()).ErrorCode).IsEqualTo(AutomationFailed);
    }

    /// <summary>Semantic matching honors the ID, name, class, control type, and ordinal rules.</summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Element_matching_uses_stable_fallback_order()
    {
        var byId = new ElementIdentity("Button", "save", "Save", "Button");
        var byName = new ElementIdentity("Text", string.Empty, "Status", "TextBlock");
        var byClass = new ElementIdentity("Pane", string.Empty, string.Empty, "CustomPane");
        var siblings = new[] { byId, byName, byId, byClass };

        await Assert.That(ElementMatching.Matches(byId, byId)).IsTrue();
        await Assert.That(ElementMatching.Matches(byId with { ControlType = "Text" }, byId)).IsFalse();
        await Assert.That(ElementMatching.Matches(byId with { AutomationId = "other" }, byId)).IsFalse();
        await Assert.That(ElementMatching.Matches(byName, byName)).IsTrue();
        await Assert.That(ElementMatching.Matches(byName with { Name = "Other" }, byName)).IsFalse();
        await Assert.That(ElementMatching.Matches(byClass, byClass)).IsTrue();
        await Assert.That(ElementMatching.Matches(byClass with { ClassName = "Other" }, byClass)).IsFalse();
        await Assert.That(ElementMatching.CountPriorMatches(siblings, OtherProcessId, byId)).IsEqualTo(1);
        await Assert.That(ElementMatching.CountPriorMatches(siblings, 0, byId)).IsEqualTo(0);
    }

    /// <summary>Window eligibility and attachment invariants reject unusable roots.</summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Backend_window_invariants_are_explicit()
    {
        var identity = CreateIdentity();

        await Assert.That(FlaUiAutomationBackend.IsDiscoverable(identity, WindowHandle)).IsTrue();
        await Assert.That(FlaUiAutomationBackend.IsDiscoverable(null, WindowHandle)).IsFalse();
        await Assert.That(FlaUiAutomationBackend.IsDiscoverable(identity, 0)).IsFalse();
        FlaUiAutomationBackend.ValidateAttachedWindow(ProcessId, ProcessId, WindowHandle);
        await Assert.That(static () => FlaUiAutomationBackend.ValidateAttachedWindow(OtherProcessId, ProcessId, WindowHandle))
            .Throws<InvalidOperationException>();
        await Assert.That(static () => FlaUiAutomationBackend.ValidateAttachedWindow(ProcessId, ProcessId, 0))
            .Throws<InvalidOperationException>();
        await Assert.That(static () => new FlaUiAutomationBackend(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Process resolution maps inaccessible or terminated processes to absence.</summary>
    /// <returns>A task representing the assertions.</returns>
    [Test]
    public async Task Process_identity_provider_handles_all_expected_failures()
    {
        var identity = CreateIdentity();
        var success = new WindowsProcessIdentityProvider(CreateIdentity);
        var argumentFailure = new WindowsProcessIdentityProvider(static _ => throw new ArgumentException());
        var operationFailure = new WindowsProcessIdentityProvider(static _ => throw new InvalidOperationException());
        var windowsFailure = new WindowsProcessIdentityProvider(static _ => throw new Win32Exception());

        await Assert.That((await success.ResolveAsync(ProcessId, CancellationToken.None))!.ProcessName)
            .IsEqualTo(identity.ProcessName);
        await Assert.That(await argumentFailure.ResolveAsync(ProcessId, CancellationToken.None)).IsNull();
        await Assert.That(await operationFailure.ResolveAsync(ProcessId, CancellationToken.None)).IsNull();
        await Assert.That(await windowsFailure.ResolveAsync(ProcessId, CancellationToken.None)).IsNull();
        await Assert.That(static () => new WindowsProcessIdentityProvider(null!)).Throws<ArgumentNullException>();

        using var current = Process.GetCurrentProcess();
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(() => current.MainModule))
            .IsEqualTo(current.MainModule!.FileName);
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(static () => null)).IsEqualTo(string.Empty);
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(
            static () => throw new InvalidOperationException())).IsEqualTo(string.Empty);
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(
            static () => throw new Win32Exception())).IsEqualTo(string.Empty);
    }

    /// <summary>Creates a deterministic identity for backend invariants.</summary>
    /// <returns>The identity used by these unit tests.</returns>
    private static ProcessIdentity CreateIdentity() => new(
        ProcessId,
        TimeProvider.System.GetUtcNow(),
        ProcessName,
        ExecutableName,
        ProcessId);

    /// <summary>Creates a deterministic identity from a provider process ID callback.</summary>
    /// <param name="processId">The provider process ID.</param>
    /// <returns>The corresponding deterministic identity.</returns>
    private static ProcessIdentity CreateIdentity(int processId) => CreateIdentity() with { ProcessId = processId };

    /// <summary>Represents a provider COM failure without depending on a live provider.</summary>
    private sealed class TestComException : COMException
    {
        /// <summary>Initializes a new instance of the <see cref="TestComException"/> class.</summary>
        public TestComException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TestComException"/> class.</summary>
        /// <param name="message">The exception message.</param>
        public TestComException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TestComException"/> class.</summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The nested exception.</param>
        public TestComException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

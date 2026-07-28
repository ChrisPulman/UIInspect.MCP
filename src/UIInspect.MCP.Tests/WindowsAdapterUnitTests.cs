// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
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
    /// <summary>Expected UIA read and action exceptions become deterministic safe fallbacks.</summary>
    [Test]
    public async Task Uia_operation_guard_translates_expected_provider_failures()
    {
        var unsupported = (PropertyNotSupportedException)RuntimeHelpers.GetUninitializedObject(
            typeof(PropertyNotSupportedException));

        await Assert.That(UiaOperationGuard.Read(() => 7, -1)).IsEqualTo(7);
        await Assert.That(UiaOperationGuard.Read<int>(() => throw new InvalidOperationException(), -1)).IsEqualTo(-1);
        await Assert.That(UiaOperationGuard.Read<int>(() => throw new TestComException(), -2)).IsEqualTo(-2);
        await Assert.That(UiaOperationGuard.Read<int>(() => throw unsupported, -3)).IsEqualTo(-3);
        await Assert.That(UiaOperationGuard.ReadString(() => "value")).IsEqualTo("value");
        await Assert.That(UiaOperationGuard.ReadString(() => null)).IsEqualTo(string.Empty);
        await Assert.That(UiaOperationGuard.Execute(() => PlatformActionResult.Ok("ok")).Succeeded).IsTrue();
        await Assert.That(UiaOperationGuard.Execute(() => throw new InvalidOperationException()).ErrorCode)
            .IsEqualTo("automation_failed");
        await Assert.That(UiaOperationGuard.Execute(() => throw new TestComException()).ErrorCode)
            .IsEqualTo("automation_failed");
    }

    /// <summary>Semantic matching honors the ID, name, class, control type, and ordinal rules.</summary>
    [Test]
    public async Task Element_matching_uses_stable_fallback_order()
    {
        var byId = new ElementIdentity("Button", "save", "Save", "Button");
        var byName = new ElementIdentity("Text", string.Empty, "Status", "TextBlock");
        var byClass = new ElementIdentity("Pane", string.Empty, string.Empty, "CustomPane");
        var siblings = new[]
        {
            byId,
            byName,
            byId,
            byClass,
        };

        await Assert.That(ElementMatching.Matches(byId, byId)).IsTrue();
        await Assert.That(ElementMatching.Matches(byId with { ControlType = "Text" }, byId)).IsFalse();
        await Assert.That(ElementMatching.Matches(byId with { AutomationId = "other" }, byId)).IsFalse();
        await Assert.That(ElementMatching.Matches(byName, byName)).IsTrue();
        await Assert.That(ElementMatching.Matches(byName with { Name = "Other" }, byName)).IsFalse();
        await Assert.That(ElementMatching.Matches(byClass, byClass)).IsTrue();
        await Assert.That(ElementMatching.Matches(byClass with { ClassName = "Other" }, byClass)).IsFalse();
        await Assert.That(ElementMatching.CountPriorMatches(siblings, 2, byId)).IsEqualTo(1);
        await Assert.That(ElementMatching.CountPriorMatches(siblings, 0, byId)).IsEqualTo(0);
    }

    /// <summary>Window eligibility and attachment invariants reject unusable roots.</summary>
    [Test]
    public async Task Backend_window_invariants_are_explicit()
    {
        var identity = new ProcessIdentity(1, DateTimeOffset.UtcNow, "app", "app.exe", 1);

        await Assert.That(FlaUiAutomationBackend.IsDiscoverable(identity, 1)).IsTrue();
        await Assert.That(FlaUiAutomationBackend.IsDiscoverable(null, 1)).IsFalse();
        await Assert.That(FlaUiAutomationBackend.IsDiscoverable(identity, 0)).IsFalse();
        FlaUiAutomationBackend.ValidateAttachedWindow(1, 1, 1);
        await Assert.That(() => FlaUiAutomationBackend.ValidateAttachedWindow(2, 1, 1))
            .Throws<InvalidOperationException>();
        await Assert.That(() => FlaUiAutomationBackend.ValidateAttachedWindow(1, 1, 0))
            .Throws<InvalidOperationException>();
        await Assert.That(() => new FlaUiAutomationBackend(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Process resolution maps inaccessible or terminated processes to absence.</summary>
    [Test]
    public async Task Process_identity_provider_handles_all_expected_failures()
    {
        var identity = new ProcessIdentity(1, DateTimeOffset.UtcNow, "app", "app.exe", 1);
        var success = new WindowsProcessIdentityProvider(_ => identity);
        var argumentFailure = new WindowsProcessIdentityProvider(
            _ => throw new ArgumentException());
        var operationFailure = new WindowsProcessIdentityProvider(
            _ => throw new InvalidOperationException());
        var windowsFailure = new WindowsProcessIdentityProvider(
            _ => throw new Win32Exception());

        await Assert.That(await success.ResolveAsync(1, CancellationToken.None)).IsEqualTo(identity);
        await Assert.That(await argumentFailure.ResolveAsync(1, CancellationToken.None)).IsNull();
        await Assert.That(await operationFailure.ResolveAsync(1, CancellationToken.None)).IsNull();
        await Assert.That(await windowsFailure.ResolveAsync(1, CancellationToken.None)).IsNull();
        await Assert.That(() => new WindowsProcessIdentityProvider(null!)).Throws<ArgumentNullException>();

        using var current = Process.GetCurrentProcess();
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(() => current.MainModule))
            .IsEqualTo(current.MainModule!.FileName);
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(() => null))
            .IsEqualTo(string.Empty);
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(
            () => throw new InvalidOperationException())).IsEqualTo(string.Empty);
        await Assert.That(WindowsProcessIdentityProvider.ReadExecutablePath(
            () => throw new Win32Exception())).IsEqualTo(string.Empty);
    }

    private sealed class TestComException : COMException;
}

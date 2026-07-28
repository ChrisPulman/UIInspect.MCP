// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.InteropServices;
using FlaUI.Core.Exceptions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Automation;

/// <summary>Contains deterministic translations for expected UI Automation provider failures.</summary>
internal static class UiaOperationGuard
{
    /// <summary>Safe error code for rejected or unavailable UI Automation operations.</summary>
    private const string AutomationFailedCode = "automation_failed";

    /// <summary>Reads a UI Automation value, translating transient provider failures to a deterministic fallback.</summary>
    /// <typeparam name="T">Value type returned by the provider.</typeparam>
    /// <param name="read">Provider read operation.</param>
    /// <param name="fallback">Value returned for expected provider failures.</param>
    /// <returns>The provider value or <paramref name="fallback"/>.</returns>
    internal static T Read<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
        catch (COMException)
        {
            return fallback;
        }
        catch (PropertyNotSupportedException)
        {
            return fallback;
        }
    }

    /// <summary>Reads a nullable UI Automation string, normalizing absent values to an empty string.</summary>
    /// <param name="read">Provider string read operation.</param>
    /// <returns>A non-null string value.</returns>
    internal static string ReadString(Func<string?> read) =>
        Read(read, null) ?? string.Empty;

    /// <summary>Executes a UI Automation action and translates expected provider failures into an action result.</summary>
    /// <param name="operation">Provider action.</param>
    /// <returns>The action result or a deterministic automation failure.</returns>
    internal static PlatformActionResult Execute(Func<PlatformActionResult> operation)
    {
        try
        {
            return operation();
        }
        catch (InvalidOperationException)
        {
            return PlatformActionResult.Fail(
                AutomationFailedCode,
                "The UI Automation provider rejected the operation.");
        }
        catch (COMException)
        {
            return PlatformActionResult.Fail(
                AutomationFailedCode,
                "The UI Automation element became unavailable.");
        }
        catch (ArgumentException)
        {
            return PlatformActionResult.Fail(
                AutomationFailedCode,
                "The UI Automation provider rejected the supplied value.");
        }
    }
}

// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Runtime.InteropServices;
using FlaUI.Core.Exceptions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Automation;

/// <summary>Contains deterministic translations for expected UI Automation provider failures.</summary>
internal static class UiaOperationGuard
{
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

    internal static string ReadString(Func<string?> read) =>
        Read(read, null) ?? string.Empty;

    internal static PlatformActionResult Execute(Func<PlatformActionResult> operation)
    {
        try
        {
            return operation();
        }
        catch (InvalidOperationException)
        {
            return PlatformActionResult.Fail(
                "automation_failed",
                "The UI Automation provider rejected the operation.");
        }
        catch (COMException)
        {
            return PlatformActionResult.Fail(
                "automation_failed",
                "The UI Automation element became unavailable.");
        }
    }
}

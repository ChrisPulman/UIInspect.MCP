// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Diagnostics;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Processes;

/// <summary>Resolves exact Windows process instances with PID-reuse protection.</summary>
public sealed class WindowsProcessIdentityProvider : IProcessIdentityProvider
{
    private readonly Func<int, ProcessIdentity?> _resolve;

    /// <summary>Initializes a resolver backed by the operating-system process table.</summary>
    public WindowsProcessIdentityProvider()
        : this(ResolveProcess)
    {
    }

    internal WindowsProcessIdentityProvider(Func<int, ProcessIdentity?> resolve) =>
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));

    /// <inheritdoc/>
    public ValueTask<ProcessIdentity?> ResolveAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (processId <= 0)
        {
            return ValueTask.FromResult<ProcessIdentity?>(null);
        }

        try
        {
            return ValueTask.FromResult(_resolve(processId));
        }
        catch (ArgumentException)
        {
            return ValueTask.FromResult<ProcessIdentity?>(null);
        }
        catch (InvalidOperationException)
        {
            return ValueTask.FromResult<ProcessIdentity?>(null);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return ValueTask.FromResult<ProcessIdentity?>(null);
        }
    }

    internal static string ReadExecutablePath(Func<ProcessModule?> read)
    {
        try
        {
            return read()?.FileName ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    private static ProcessIdentity ResolveProcess(int processId)
    {
        using var process = Process.GetProcessById(processId);
        return new ProcessIdentity(
            process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime()),
            process.ProcessName,
            ReadExecutablePath(() => process.MainModule),
            process.SessionId);
    }
}

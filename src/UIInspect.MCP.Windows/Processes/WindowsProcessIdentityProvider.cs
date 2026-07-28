// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Processes;

/// <summary>Resolves exact Windows process instances with PID-reuse protection.</summary>
public sealed class WindowsProcessIdentityProvider : IProcessIdentityProvider
{
    /// <summary>Injectable process resolver used to isolate process access and test failure handling.</summary>
    private readonly Func<int, ProcessIdentity?> _resolve;

    /// <summary>Initializes a new instance of the <see cref="WindowsProcessIdentityProvider"/> class.</summary>
    public WindowsProcessIdentityProvider()
        : this(ResolveProcess)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WindowsProcessIdentityProvider"/> class with a process resolver.</summary>
    /// <param name="resolve">Resolver that returns an exact process identity or throws an expected process-access exception.</param>
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

    /// <summary>Reads an executable path while normalizing access-denied and exited-process failures.</summary>
    /// <param name="read">Process-module access operation.</param>
    /// <returns>The executable path or an empty string when unavailable.</returns>
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

    /// <summary>Constructs a PID-reuse-resistant identity from the live Windows process.</summary>
    /// <param name="processId">Windows process identifier.</param>
    /// <returns>The resolved identity.</returns>
    private static ProcessIdentity ResolveProcess(int processId)
    {
        using var process = Process.GetProcessById(processId);
        return new(
            process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime()),
            process.ProcessName,
            ReadExecutablePath(() => process.MainModule),
            process.SessionId);
    }
}

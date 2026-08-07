// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace UIInspect.MCP.Windows.Security;

/// <summary>Names local broker objects for one Windows user and interactive session.</summary>
/// <param name="SessionId">Interactive Windows session identifier.</param>
/// <param name="PipeName">Current-user-only named pipe.</param>
/// <param name="MarkerName">Process-lifetime singleton marker.</param>
internal sealed record UnattendedApprovalScope(int SessionId, string PipeName, string MarkerName)
{
    /// <summary>Characters retained from the user SID hash.</summary>
    private const int ScopeHashCharacters = 24;

    /// <summary>Create the current Windows user/session scope.</summary>
    /// <returns>Current scope.</returns>
    internal static UnattendedApprovalScope CreateCurrent()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value
            ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sid)))[..ScopeHashCharacters];
        using var process = Process.GetCurrentProcess();
        var sessionId = process.SessionId;
        var key = $"{hash}.{sessionId}";
        return new(
            sessionId,
            $"UIInspect.MCP.UnattendedApproval.{key}",
            $"Local\\UIInspect.MCP.UnattendedApproval.{key}");
    }
}

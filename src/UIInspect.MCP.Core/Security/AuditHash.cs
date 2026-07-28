// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Security.Cryptography;
using System.Text;

namespace UIInspect.MCP.Core.Security;

/// <summary>Produces stable non-secret identities for audit and rate-limit keys.</summary>
public static class AuditHash
{
    /// <summary>Hash a client identifier without retaining the original value.</summary>
    /// <param name="value">Client identifier.</param>
    /// <returns>Lower-case SHA-256 hexadecimal text.</returns>
    public static string Compute(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}

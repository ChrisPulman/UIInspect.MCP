// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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

    /// <summary>Compare two client hashes without leaking a matching prefix through timing.</summary>
    /// <param name="first">First hash.</param>
    /// <param name="second">Second hash.</param>
    /// <returns>True when the hashes match.</returns>
    public static bool Matches(string first, string second) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(first), Encoding.UTF8.GetBytes(second));
}

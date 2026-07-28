// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UIInspect.MCP.Server.Serialization;

/// <summary>Provides stable MCP JSON output.</summary>
public static class JsonOutput
{
    /// <summary>Serializer configuration shared by all MCP tool responses.</summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true };

    /// <summary>Serialize an MCP tool result.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="value">Result value.</param>
    /// <returns>JSON text.</returns>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}

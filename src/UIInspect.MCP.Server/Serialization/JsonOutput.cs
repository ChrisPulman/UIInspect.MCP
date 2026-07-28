// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UIInspect.MCP.Server.Serialization;

/// <summary>Provides stable MCP JSON output.</summary>
public static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    /// <summary>Serialize an MCP tool result.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="value">Result value.</param>
    /// <returns>JSON text.</returns>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}

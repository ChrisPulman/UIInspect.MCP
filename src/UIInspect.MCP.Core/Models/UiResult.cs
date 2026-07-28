// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Uniform safe result envelope exposed by MCP tools.</summary>
/// <typeparam name="T">Payload type.</typeparam>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Data">Success payload.</param>
/// <param name="Error">Safe failure payload.</param>
public sealed record UiResult<T>(bool Success, T? Data, UiError? Error)
{
    /// <summary>Create a successful result.</summary>
    /// <param name="data">Payload.</param>
    /// <returns>Successful result.</returns>
    public static UiResult<T> Ok(T data) => new(true, data, null);

    /// <summary>Create a failed result.</summary>
    /// <param name="code">Stable code.</param>
    /// <param name="message">Safe message.</param>
    /// <returns>Failed result.</returns>
    public static UiResult<T> Fail(string code, string message) => Fail(code, message, null);

    /// <summary>Create a failed result with a retry delay.</summary>
    /// <param name="code">Stable code.</param>
    /// <param name="message">Safe message.</param>
    /// <param name="retryAfter">Optional retry delay.</param>
    /// <returns>Failed result.</returns>
    public static UiResult<T> Fail(string code, string message, TimeSpan? retryAfter) => new(false, default, new UiError(code, message, retryAfter?.TotalMilliseconds));
}

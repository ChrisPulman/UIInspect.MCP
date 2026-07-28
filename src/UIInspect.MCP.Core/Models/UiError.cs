// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Safe operation error.</summary>
/// <param name="Code">Stable machine-readable code.</param>
/// <param name="Message">Safe user-facing message.</param>
/// <param name="RetryAfterMilliseconds">Optional retry delay for rate limits.</param>
public sealed record UiError(string Code, string Message, double? RetryAfterMilliseconds = null);

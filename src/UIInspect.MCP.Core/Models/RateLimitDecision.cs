// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Rate limit decision.</summary>
/// <param name="IsAllowed">Whether the operation may continue.</param>
/// <param name="RetryAfter">Delay until another permit may be available.</param>
public sealed record RateLimitDecision(bool IsAllowed, TimeSpan RetryAfter);

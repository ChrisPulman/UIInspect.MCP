// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>Applies a bounded operation rate policy.</summary>
public interface IOperationRateLimiter
{
    /// <summary>Try to acquire one operation permit.</summary>
    /// <param name="bucket">Non-secret bucket identity.</param>
    /// <param name="permitLimit">Maximum permits per window.</param>
    /// <param name="window">Rate window.</param>
    /// <returns>Rate decision.</returns>
    RateLimitDecision TryAcquire(string bucket, int permitLimit, TimeSpan window);
}

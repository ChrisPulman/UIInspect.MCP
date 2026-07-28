// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>Queue-driven rate limiter for deterministic policy tests.</summary>
internal sealed class FakeRateLimiter : IOperationRateLimiter
{
    /// <summary>Gets queued rate-limit decisions.</summary>
    internal Queue<RateLimitDecision> Decisions { get; } = new();

    /// <summary>Gets observed rate-limit bucket names.</summary>
    internal List<string> Buckets { get; } = [];

    /// <inheritdoc/>
    public RateLimitDecision TryAcquire(string bucket, int permitLimit, TimeSpan window)
    {
        Buckets.Add(bucket);
        return Decisions.TryDequeue(out var decision)
            ? decision
            : new RateLimitDecision(true, TimeSpan.Zero);
    }
}

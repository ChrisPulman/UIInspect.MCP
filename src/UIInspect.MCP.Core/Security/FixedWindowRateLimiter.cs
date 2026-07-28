// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.InteropServices;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Security;

/// <summary>A small in-memory fixed-window limiter suitable for one local stdio server.</summary>
public sealed class FixedWindowRateLimiter : IOperationRateLimiter
{
    /// <summary>Timestamp queues grouped by non-secret rate bucket.</summary>
    private readonly Dictionary<string, Queue<DateTimeOffset>> _buckets = new(StringComparer.Ordinal);

    /// <summary>Serializes access to the bucket dictionary and its queues.</summary>
    private readonly Lock _gate = new();

    /// <summary>Provides current UTC time.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="FixedWindowRateLimiter"/> class.</summary>
    /// <param name="timeProvider">Clock.</param>
    public FixedWindowRateLimiter(TimeProvider timeProvider) =>
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public RateLimitDecision TryAcquire(string bucket, int permitLimit, TimeSpan window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            ref var timestamps = ref CollectionsMarshal.GetValueRefOrAddDefault(_buckets, bucket, out var exists);
            if (!exists)
            {
                timestamps = [];
            }

            var queue = timestamps!;
            while (queue.TryPeek(out var oldest) && now - oldest >= window)
            {
                _ = queue.Dequeue();
            }

            if (queue.Count >= permitLimit)
            {
                var retryAfter = window - (now - queue.Peek());
                return new(false, retryAfter);
            }

            queue.Enqueue(now);
            return new(true, TimeSpan.Zero);
        }
    }
}

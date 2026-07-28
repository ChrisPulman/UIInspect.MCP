// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Security;

/// <summary>A small in-memory fixed-window limiter suitable for one local stdio server.</summary>
public sealed class FixedWindowRateLimiter : IOperationRateLimiter
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _buckets = new(StringComparer.Ordinal);
    private readonly object _gate = new();
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
            if (!_buckets.TryGetValue(bucket, out var timestamps))
            {
                timestamps = new Queue<DateTimeOffset>();
                _buckets.Add(bucket, timestamps);
            }

            while (timestamps.TryPeek(out var oldest) && now - oldest >= window)
            {
                _ = timestamps.Dequeue();
            }

            if (timestamps.Count >= permitLimit)
            {
                var retryAfter = window - (now - timestamps.Peek());
                return new RateLimitDecision(false, retryAfter);
            }

            timestamps.Enqueue(now);
            return new RateLimitDecision(true, TimeSpan.Zero);
        }
    }
}

// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Security;

/// <summary>Stores short-lived consent grants for exact process instances.</summary>
public sealed class ConsentRegistry
{
    private readonly ConcurrentDictionary<Guid, ConsentGrant> _grants = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="ConsentRegistry"/> class.</summary>
    /// <param name="timeProvider">Clock.</param>
    public ConsentRegistry(TimeProvider timeProvider) =>
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>Create a grant.</summary>
    /// <param name="clientHash">Hashed client identity.</param>
    /// <param name="target">Exact process instance.</param>
    /// <param name="capabilities">Granted capabilities.</param>
    /// <param name="duration">Grant duration.</param>
    /// <returns>Created grant.</returns>
    public ConsentGrant Grant(
        string clientHash,
        ProcessIdentity target,
        UiCapability capabilities,
        TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientHash);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        var now = _timeProvider.GetUtcNow();
        var grant = new ConsentGrant(
            Guid.NewGuid(),
            clientHash,
            target,
            capabilities,
            now,
            now.Add(duration));
        _ = _grants.TryAdd(grant.Id, grant);
        return grant;
    }

    /// <summary>Find an active matching grant.</summary>
    /// <param name="clientHash">Hashed client identity.</param>
    /// <param name="target">Exact process instance.</param>
    /// <param name="requiredCapability">Required capability.</param>
    /// <returns>Matching grant, or null.</returns>
    public ConsentGrant? FindActive(
        string clientHash,
        ProcessIdentity target,
        UiCapability requiredCapability)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _grants)
        {
            var grant = pair.Value;
            if (grant.ExpiresAtUtc <= now)
            {
                _ = _grants.TryRemove(pair.Key, out _);
                continue;
            }

            if (grant.ClientHash == clientHash &&
                grant.Target == target &&
                grant.Capabilities.HasFlag(requiredCapability))
            {
                return grant;
            }
        }

        return null;
    }

    /// <summary>Revoke one grant.</summary>
    /// <param name="consentId">Grant identifier.</param>
    /// <returns>True when removed.</returns>
    public bool Revoke(Guid consentId) => _grants.TryRemove(consentId, out _);

    /// <summary>Revoke grants bound to a target process instance.</summary>
    /// <param name="target">Exact process instance.</param>
    /// <returns>Number removed.</returns>
    public int RevokeTarget(ProcessIdentity target)
    {
        var removed = 0;
        foreach (var pair in _grants.Where(pair => pair.Value.Target == target))
        {
            if (_grants.TryRemove(pair.Key, out _))
            {
                removed++;
            }
        }

        return removed;
    }
}

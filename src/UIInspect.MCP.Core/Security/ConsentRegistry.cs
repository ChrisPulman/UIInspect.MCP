// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Security;

/// <summary>Stores short-lived consent grants for exact process instances.</summary>
public sealed class ConsentRegistry
{
    /// <summary>Active grants keyed by their opaque identifiers.</summary>
    private readonly ConcurrentDictionary<Guid, ConsentGrant> _grants = new();

    /// <summary>Serializes get-or-create operations so racing consent calls share one grant.</summary>
    private readonly Lock _grantGate = new();

    /// <summary>Provides current UTC time.</summary>
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

    /// <summary>Return an active matching grant or atomically create one.</summary>
    /// <param name="clientHash">Hashed client identity.</param>
    /// <param name="target">Exact process instance.</param>
    /// <param name="capabilities">Granted capabilities.</param>
    /// <param name="duration">Grant duration.</param>
    /// <returns>The existing or newly created grant.</returns>
    public ConsentGrant GrantOrGetActive(
        string clientHash,
        ProcessIdentity target,
        UiCapability capabilities,
        TimeSpan duration)
    {
        lock (_grantGate)
        {
            return FindActive(clientHash, target, capabilities)
                ?? Grant(clientHash, target, capabilities, duration);
        }
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

            if (AuditHash.Matches(grant.ClientHash, clientHash)
                && grant.Target == target
                && (grant.Capabilities & requiredCapability) == requiredCapability)
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
        foreach (var pair in _grants)
        {
            if (pair.Value.Target == target && _grants.TryRemove(pair.Key, out _))
            {
                removed++;
            }
        }

        return removed;
    }
}

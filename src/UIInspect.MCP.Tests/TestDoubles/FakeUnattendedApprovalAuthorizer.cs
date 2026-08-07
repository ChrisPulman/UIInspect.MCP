// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>Deterministic unattended approval authority for service tests.</summary>
internal sealed class FakeUnattendedApprovalAuthorizer : IUnattendedApprovalAuthorizer
{
    /// <summary>Gets or sets the current lease.</summary>
    internal UnattendedApprovalLease? Lease { get; set; }

    /// <summary>Gets or sets whether validation succeeds.</summary>
    internal bool IsValid { get; set; } = true;

    /// <summary>Gets or sets a deterministic action invoked during validation.</summary>
    internal Action? OnValidate { get; set; }

    /// <summary>Gets validation requests.</summary>
    internal ConcurrentQueue<(Guid LeaseId, ProcessIdentity Target, UiCapability Capabilities)> Validations { get; } = new();

    /// <inheritdoc/>
    public ValueTask<UnattendedApprovalLease?> GetActiveLeaseAsync(
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(
            Lease is { } lease && (lease.Capabilities & requiredCapabilities) == requiredCapabilities
                ? lease
                : null);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ValidateAsync(
        Guid leaseId,
        ProcessIdentity target,
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Validations.Enqueue((leaseId, target, requiredCapabilities));
        OnValidate?.Invoke();
        var valid = IsValid
            && Lease is { } lease
            && lease.LeaseId == leaseId
            && lease.ExpiresAtUtc > DateTimeOffset.MinValue
            && (lease.Capabilities & requiredCapabilities) == requiredCapabilities;
        return ValueTask.FromResult(valid);
    }
}

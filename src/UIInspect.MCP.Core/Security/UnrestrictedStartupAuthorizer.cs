// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Security;

/// <summary>Reports server-owner startup authorization without contacting an approval broker.</summary>
public sealed class UnrestrictedStartupAuthorizer : IUnattendedApprovalAuthorizer
{
    /// <summary>The server-local authorization, valid for the lifetime of this server.</summary>
    private readonly UnattendedApprovalLease _authorization = new(
        Guid.NewGuid(),
        UiCapability.Inspect | UiCapability.Interact | UiCapability.Keyboard,
        TimeProvider.System.GetUtcNow(),
        DateTimeOffset.MaxValue);

    /// <inheritdoc/>
    public ValueTask<UnattendedApprovalLease?> GetActiveLeaseAsync(UiCapability requiredCapabilities, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UnattendedApprovalLease? authorization =
            (_authorization.Capabilities & requiredCapabilities) == requiredCapabilities ? _authorization : null;
        return ValueTask.FromResult(authorization);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ValidateAsync(Guid leaseId, ProcessIdentity target, UiCapability requiredCapabilities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(leaseId == _authorization.LeaseId
            && (_authorization.Capabilities & requiredCapabilities) == requiredCapabilities);
    }
}

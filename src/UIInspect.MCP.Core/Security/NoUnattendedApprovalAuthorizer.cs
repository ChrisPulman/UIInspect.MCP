// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Security;

/// <summary>Fail-closed unattended approval authorizer used when no broker is configured.</summary>
public sealed class NoUnattendedApprovalAuthorizer : IUnattendedApprovalAuthorizer
{
    /// <inheritdoc/>
    public ValueTask<UnattendedApprovalLease?> GetActiveLeaseAsync(
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<UnattendedApprovalLease?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ValidateAsync(
        Guid leaseId,
        ProcessIdentity target,
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}

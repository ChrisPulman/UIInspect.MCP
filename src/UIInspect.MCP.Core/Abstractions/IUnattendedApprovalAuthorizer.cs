// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>Queries and validates an optional user-approved unattended access lease.</summary>
public interface IUnattendedApprovalAuthorizer
{
    /// <summary>Get the active lease when it includes the required capabilities.</summary>
    /// <param name="requiredCapabilities">Capabilities required by the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active lease, or <see langword="null"/> when unattended access is unavailable.</returns>
    ValueTask<UnattendedApprovalLease?> GetActiveLeaseAsync(
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken);

    /// <summary>Revalidate an unattended lease and exact target before a protected operation.</summary>
    /// <param name="leaseId">Broker-owned lease identifier.</param>
    /// <param name="target">Exact target process identity.</param>
    /// <param name="requiredCapabilities">Capabilities required by the operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> only while the broker confirms the lease and target.</returns>
    ValueTask<bool> ValidateAsync(
        Guid leaseId,
        ProcessIdentity target,
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken);
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>A broker-owned approval window shared by UIInspect servers in one user session.</summary>
/// <param name="LeaseId">Opaque lease identifier.</param>
/// <param name="Capabilities">Maximum capability ceiling.</param>
/// <param name="GrantedAtUtc">Approval time.</param>
/// <param name="ExpiresAtUtc">Hard expiry time.</param>
public sealed record UnattendedApprovalLease(
    Guid LeaseId,
    UiCapability Capabilities,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc);

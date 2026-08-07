// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Security;

/// <summary>One local consent-broker response.</summary>
/// <param name="Success">Whether the request was accepted.</param>
/// <param name="Lease">Current lease when available.</param>
/// <param name="Reason">Safe failure reason.</param>
internal sealed record UnattendedApprovalBrokerResponse(
    bool Success,
    UnattendedApprovalLease? Lease,
    string? Reason);

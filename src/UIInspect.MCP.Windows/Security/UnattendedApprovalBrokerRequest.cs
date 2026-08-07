// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Security;

/// <summary>One local consent-broker request.</summary>
/// <param name="Operation">Status, validation, or revocation operation.</param>
/// <param name="LeaseId">Optional lease identifier.</param>
/// <param name="Target">Optional exact target identity.</param>
/// <param name="Capabilities">Required capabilities.</param>
internal sealed record UnattendedApprovalBrokerRequest(
    string Operation,
    Guid? LeaseId,
    ProcessIdentity? Target,
    UiCapability Capabilities);

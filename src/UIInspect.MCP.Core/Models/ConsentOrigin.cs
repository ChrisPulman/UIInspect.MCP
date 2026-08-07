// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Identifies the trusted authority that produced a consent grant.</summary>
public enum ConsentOrigin
{
    /// <summary>A trusted per-target Windows prompt approved the grant.</summary>
    ExplicitWindowsPrompt,

    /// <summary>The user activated a Windows-session-scoped unattended approval lease.</summary>
    UnattendedApprovalLease,
}

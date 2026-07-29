// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Result of a server-session consent request.</summary>
/// <param name="IsApproved">Whether the local user approved the retained capability ceiling.</param>
/// <param name="RetryAfter">Retry delay when no prompt was shown because the new-prompt rate limit was reached.</param>
public sealed record SessionConsentDecision(bool IsApproved, TimeSpan? RetryAfter);

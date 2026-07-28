// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Outcome of a semantic UI action.</summary>
/// <param name="Action">Action name.</param>
/// <param name="ElementReference">Target element reference.</param>
/// <param name="ReferencesInvalidated">Whether a fresh inspection is required before another action.</param>
/// <param name="Message">Safe action summary.</param>
public sealed record ActionReceipt(string Action, string ElementReference, bool ReferencesInvalidated, string Message);

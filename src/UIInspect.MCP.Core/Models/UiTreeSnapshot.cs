// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>A bounded point-in-time semantic UI tree.</summary>
/// <param name="SessionId">Owning automation session.</param>
/// <param name="Generation">Element-reference generation.</param>
/// <param name="Nodes">Flattened nodes in breadth-first order.</param>
/// <param name="Truncated">Whether the requested tree exceeded its depth or node budget.</param>
public sealed record UiTreeSnapshot(string SessionId, long Generation, IReadOnlyList<UiElementNode> Nodes, bool Truncated);

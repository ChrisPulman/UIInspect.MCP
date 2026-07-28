// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>One in-memory consent grant.</summary>
/// <param name="Id">Grant identifier.</param>
/// <param name="ClientHash">Hashed client identity.</param>
/// <param name="Target">Exact target process instance.</param>
/// <param name="Capabilities">Granted capabilities.</param>
/// <param name="GrantedAtUtc">Grant time.</param>
/// <param name="ExpiresAtUtc">Expiry time.</param>
public sealed record ConsentGrant(Guid Id, string ClientHash, ProcessIdentity Target, UiCapability Capabilities, DateTimeOffset GrantedAtUtc, DateTimeOffset ExpiresAtUtc);

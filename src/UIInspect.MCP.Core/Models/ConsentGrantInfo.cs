// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Public details of a consent grant.</summary>
/// <param name="ConsentId">Grant identifier.</param>
/// <param name="Target">Exact process instance.</param>
/// <param name="Capabilities">Granted capabilities.</param>
/// <param name="ExpiresAtUtc">Expiry.</param>
/// <param name="Origin">Authority that produced the grant.</param>
public sealed record ConsentGrantInfo(
    Guid ConsentId,
    ProcessIdentity Target,
    UiCapability Capabilities,
    DateTimeOffset ExpiresAtUtc,
    ConsentOrigin Origin);

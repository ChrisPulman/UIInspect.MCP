// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>Details returned after an automation session is attached.</summary>
/// <param name="SessionId">Opaque session identifier.</param>
/// <param name="Target">Bound process instance.</param>
/// <param name="WindowHandle">Bound top-level window handle.</param>
/// <param name="ExpiresAtUtc">Consent expiry applied to the session.</param>
public sealed record AutomationSessionInfo(string SessionId, ProcessIdentity Target, long WindowHandle, DateTimeOffset ExpiresAtUtc);

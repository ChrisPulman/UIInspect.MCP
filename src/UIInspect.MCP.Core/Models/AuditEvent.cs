// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Models;

/// <summary>A redacted append-only audit event.</summary>
/// <param name="OccurredAtUtc">Event time.</param>
/// <param name="EventId">Event identifier.</param>
/// <param name="EventType">Event type.</param>
/// <param name="Outcome">Allowed, denied, failed, or completed.</param>
/// <param name="ClientHash">Hashed client identity.</param>
/// <param name="ProcessId">Optional target process ID.</param>
/// <param name="ProcessStartedAtUtc">Optional process creation time.</param>
/// <param name="Operation">Operation name.</param>
/// <param name="Reason">Safe reason code; never raw UI values.</param>
public sealed record AuditEvent(
    DateTimeOffset OccurredAtUtc,
    string EventId,
    string EventType,
    string Outcome,
    string ClientHash,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string Operation,
    string? Reason);

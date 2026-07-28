// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
namespace UIInspect.MCP.Core.Models;

/// <summary>One in-memory consent grant.</summary>
/// <param name="Id">Grant identifier.</param>
/// <param name="ClientHash">Hashed client identity.</param>
/// <param name="Target">Exact target process instance.</param>
/// <param name="Capabilities">Granted capabilities.</param>
/// <param name="GrantedAtUtc">Grant time.</param>
/// <param name="ExpiresAtUtc">Expiry time.</param>
public sealed record ConsentGrant(
    Guid Id,
    string ClientHash,
    ProcessIdentity Target,
    UiCapability Capabilities,
    DateTimeOffset GrantedAtUtc,
    DateTimeOffset ExpiresAtUtc);

/// <summary>Rate limit decision.</summary>
/// <param name="IsAllowed">Whether the operation may continue.</param>
/// <param name="RetryAfter">Delay until another permit may be available.</param>
public sealed record RateLimitDecision(bool IsAllowed, TimeSpan RetryAfter);

/// <summary>A redacted append-only audit event.</summary>
/// <param name="OccurredAtUtc">Event time.</param>
/// <param name="EventId">Event identifier.</param>
/// <param name="EventType">Event type.</param>
/// <param name="Outcome">allowed, denied, failed, or completed.</param>
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

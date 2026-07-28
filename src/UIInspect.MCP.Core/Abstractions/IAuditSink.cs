// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Abstractions;

/// <summary>Writes redacted security audit events.</summary>
public interface IAuditSink
{
    /// <summary>Write one redacted event.</summary>
    /// <param name="auditEvent">Event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completion.</returns>
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}

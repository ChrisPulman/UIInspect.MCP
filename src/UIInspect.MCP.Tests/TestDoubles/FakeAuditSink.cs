// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>In-memory audit sink for policy assertions.</summary>
internal sealed class FakeAuditSink : IAuditSink
{
    /// <summary>Thread-safe captured audit events.</summary>
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    /// <inheritdoc/>
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _events.Enqueue(auditEvent);
        return ValueTask.CompletedTask;
    }

    /// <summary>Return a stable snapshot of captured audit events.</summary>
    /// <returns>Captured events.</returns>
    internal IReadOnlyList<AuditEvent> Snapshot() => _events.ToArray();
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Tests;

/// <summary>In-memory audit sink for policy assertions.</summary>
internal sealed class FakeAuditSink : IAuditSink
{
    /// <summary>Gets captured audit events.</summary>
    internal List<AuditEvent> Events { get; } = [];

    /// <inheritdoc/>
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Events.Add(auditEvent);
        return ValueTask.CompletedTask;
    }
}

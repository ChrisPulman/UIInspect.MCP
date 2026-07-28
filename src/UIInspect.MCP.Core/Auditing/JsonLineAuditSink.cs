// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Text.Json;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Auditing;

/// <summary>Writes redacted audit events as append-only JSON lines.</summary>
public sealed class JsonLineAuditSink : IAuditSink, IDisposable
{
    /// <summary>Serializer options for compact web-compatible JSON lines.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes writers and disposal.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Absolute append-only audit file path.</summary>
    private readonly string _path;

    /// <summary>Indicates whether the sink has been disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="JsonLineAuditSink"/> class.</summary>
    /// <param name="path">Audit JSONL path.</param>
    public JsonLineAuditSink(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(auditEvent);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var line = JsonSerializer.Serialize(auditEvent, JsonOptions) + Environment.NewLine;
            await File.AppendAllTextAsync(_path, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Dispose();
        _disposed = true;
    }
}

// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Text.Json;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Auditing;

/// <summary>Writes redacted audit events as append-only JSON lines.</summary>
public sealed class JsonLineAuditSink : IAuditSink, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;
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

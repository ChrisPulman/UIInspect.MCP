// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Windows.Security;

/// <summary>Queries the current-user consent broker over a local named pipe.</summary>
public sealed class WindowsUnattendedApprovalAuthorizer : IUnattendedApprovalAuthorizer
{
    /// <summary>Maximum time allowed for one local broker connection.</summary>
    private const int BrokerConnectionTimeoutMilliseconds = 750;

    /// <summary>Maximum time allowed for the complete broker request and response.</summary>
    private const int BrokerOperationTimeoutMilliseconds = 2000;

    /// <summary>Maximum accepted response length.</summary>
    private const int MaximumResponseCharacters = 16 * 1024;

    /// <summary>Compact broker protocol serializer settings.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Current Windows user/session broker scope.</summary>
    private readonly UnattendedApprovalScope _scope;

    /// <summary>Initializes a new instance of the <see cref="WindowsUnattendedApprovalAuthorizer"/> class.</summary>
    public WindowsUnattendedApprovalAuthorizer() => _scope = UnattendedApprovalScope.CreateCurrent();

    /// <summary>Initializes a new instance of the <see cref="WindowsUnattendedApprovalAuthorizer"/> class for an explicit scope.</summary>
    /// <param name="scope">Broker scope.</param>
    internal WindowsUnattendedApprovalAuthorizer(UnattendedApprovalScope scope) =>
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));

    /// <inheritdoc/>
    public async ValueTask<UnattendedApprovalLease?> GetActiveLeaseAsync(
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            new("status", null, null, requiredCapabilities),
            cancellationToken).ConfigureAwait(false);
        return response is { Success: true, Lease: { } lease }
            && (lease.Capabilities & requiredCapabilities) == requiredCapabilities
            ? lease
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ValidateAsync(
        Guid leaseId,
        ProcessIdentity target,
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var response = await SendAsync(
            new("validate", leaseId, target, requiredCapabilities),
            cancellationToken).ConfigureAwait(false);
        return response is { Success: true };
    }

    /// <summary>Get the current broker status for CLI and diagnostic surfaces.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active lease, or <see langword="null"/>.</returns>
    public ValueTask<UnattendedApprovalLease?> GetStatusAsync(CancellationToken cancellationToken) =>
        GetActiveLeaseAsync(UiCapability.Inspect, cancellationToken);

    /// <summary>Revoke the current lease. Revocation is intentionally available without an approval prompt.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the broker accepted revocation.</returns>
    public async ValueTask<bool> RevokeAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            new("revoke", null, null, UiCapability.None),
            cancellationToken).ConfigureAwait(false);
        return response is { Success: true };
    }

    /// <summary>Send one bounded request to the current-user broker.</summary>
    /// <param name="request">Request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Broker response, or <see langword="null"/> on any unavailable/fail-closed path.</returns>
    private ValueTask<UnattendedApprovalBrokerResponse?> SendAsync(
        UnattendedApprovalBrokerRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = Task.Factory.StartNew(
            static state =>
            {
                var send = (BrokerSendState)state!;
                return send.Owner.Send(send.Request, send.CancellationToken);
            },
            new BrokerSendState(this, request, cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        return new(task);
    }

    /// <summary>Perform one bounded synchronous exchange on its dedicated transport thread.</summary>
    /// <param name="request">Request.</param>
    /// <param name="cancellationToken">Caller cancellation token.</param>
    /// <returns>Broker response, or <see langword="null"/> on an unavailable/fail-closed path.</returns>
    private UnattendedApprovalBrokerResponse? Send(
        UnattendedApprovalBrokerRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!EventWaitHandle.TryOpenExisting(_scope.MarkerName, out var brokerMarker))
        {
            return null;
        }

        brokerMarker.Dispose();
        using var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationTimeout.CancelAfter(TimeSpan.FromMilliseconds(BrokerOperationTimeoutMilliseconds));
        var operationToken = operationTimeout.Token;
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                _scope.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            using var cancellationRegistration = operationToken.Register(
                static state => ((NamedPipeClientStream)state!).Dispose(),
                pipe);
            pipe.Connect(BrokerConnectionTimeoutMilliseconds);
            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true, };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            var serialized = JsonSerializer.Serialize(request, JsonOptions);
            writer.WriteLine(serialized);
            var line = reader.ReadLine();
            return string.IsNullOrWhiteSpace(line) || line.Length > MaximumResponseCharacters
                ? null
                : JsonSerializer.Deserialize<UnattendedApprovalBrokerResponse>(line, JsonOptions);
        }
        catch (Exception exception) when (exception is
            IOException or
            TimeoutException or
            UnauthorizedAccessException or
            JsonException or
            ObjectDisposedException or
            OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    /// <summary>State passed to a dedicated broker transport thread.</summary>
    /// <param name="Owner">Authorizer that owns the broker scope.</param>
    /// <param name="Request">Broker request.</param>
    /// <param name="CancellationToken">Caller cancellation token.</param>
    private sealed record BrokerSendState(
        WindowsUnattendedApprovalAuthorizer Owner,
        UnattendedApprovalBrokerRequest Request,
        CancellationToken CancellationToken);
}

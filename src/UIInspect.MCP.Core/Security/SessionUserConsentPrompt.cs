// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;

namespace UIInspect.MCP.Core.Security;

/// <summary>Shows each trusted consent prompt at most once per client and exact target during one server session.</summary>
public sealed class SessionUserConsentPrompt : ISessionUserConsentPrompt
{
    /// <summary>Rate window for genuinely new native prompts.</summary>
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    /// <summary>Session decisions keyed by the initiating client and exact process instance.</summary>
    private readonly ConcurrentDictionary<ConsentSessionKey, ConsentSessionDecision> _decisions = new();

    /// <summary>Displays the underlying trusted prompt.</summary>
    private readonly IUserConsentPrompt _innerPrompt;

    /// <summary>Limits genuinely new prompts without charging decision replays.</summary>
    private readonly IOperationRateLimiter _rateLimiter;

    /// <summary>Safety options controlling the new-prompt rate.</summary>
    private readonly UiInspectOptions _options;

    /// <summary>Initializes a new instance of the <see cref="SessionUserConsentPrompt"/> class.</summary>
    /// <param name="innerPrompt">Trusted platform prompt to display once.</param>
    /// <param name="rateLimiter">Rate limiter charged only for a genuinely new native prompt.</param>
    /// <param name="options">Consent prompt safety options.</param>
    public SessionUserConsentPrompt(
        IUserConsentPrompt innerPrompt,
        IOperationRateLimiter rateLimiter,
        UiInspectOptions options)
    {
        _innerPrompt = innerPrompt ?? throw new ArgumentNullException(nameof(innerPrompt));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public ValueTask<SessionConsentDecision> RequestAsync(
        ProcessIdentity target,
        UiCapability capabilities,
        string clientId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        cancellationToken.ThrowIfCancellationRequested();

        var key = new ConsentSessionKey(clientId, target);
        var candidate = new ConsentSessionDecision(
            capabilities,
            new(
                () => RequestOnceAsync(key, target, capabilities, clientId),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var decision = _decisions.GetOrAdd(
            key,
            static (_, state) => state,
            candidate);

        return (decision.Capabilities & capabilities) == capabilities
            ? AwaitDecisionAsync(key, decision, cancellationToken)
            : ValueTask.FromResult(new SessionConsentDecision(false, null));
    }

    /// <summary>Await one shared prompt decision while allowing each caller to cancel only its own wait.</summary>
    /// <param name="key">Session decision key.</param>
    /// <param name="decision">Shared server-session decision entry.</param>
    /// <param name="cancellationToken">Caller wait cancellation token.</param>
    /// <returns>The shared decision.</returns>
    private async ValueTask<SessionConsentDecision> AwaitDecisionAsync(
        ConsentSessionKey key,
        ConsentSessionDecision decision,
        CancellationToken cancellationToken)
    {
        var result = await decision.Decision.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (result.RetryAfter is not null || !result.IsTerminal)
        {
            _ = _decisions.TryRemove(new(key, decision));
        }

        return result;
    }

    /// <summary>Display the trusted prompt independently of any one request's lifetime.</summary>
    /// <param name="key">Exact session decision key.</param>
    /// <param name="target">Exact target process instance.</param>
    /// <param name="capabilities">Capabilities requested by the first caller.</param>
    /// <param name="clientId">Initiating client identity.</param>
    /// <returns>The terminal prompt decision.</returns>
    private async Task<SessionConsentDecision> RequestOnceAsync(
        ConsentSessionKey key,
        ProcessIdentity target,
        UiCapability capabilities,
        string clientId)
    {
        var clientHash = AuditHash.Compute(clientId);
        var rateDecision = _rateLimiter.TryAcquire(
            $"consent:{clientHash}",
            _options.ConsentPromptRatePerMinute,
            RateWindow);
        if (!rateDecision.IsAllowed)
        {
            return new(false, rateDecision.RetryAfter);
        }

        using var promptTimeout = new CancellationTokenSource(_options.ConsentPromptTimeout);
        try
        {
            var approved = await _innerPrompt.RequestAsync(
                target,
                capabilities,
                clientId,
                promptTimeout.Token).ConfigureAwait(false);
            return new(approved, null);
        }
        catch (OperationCanceledException)
        {
            _ = _decisions.TryRemove(key, out _);
            return new(false, null, false);
        }
    }

    /// <summary>Identifies one consent session without allowing PID reuse or cross-client reuse.</summary>
    /// <param name="ClientId">Initiating client identity.</param>
    /// <param name="Target">Exact process instance.</param>
    private sealed record ConsentSessionKey(string ClientId, ProcessIdentity Target);

    /// <summary>Stores the maximum approved scope and the one shared decision task.</summary>
    /// <param name="Capabilities">Capabilities shown in the first prompt.</param>
    /// <param name="Decision">Lazy single-flight decision.</param>
    private sealed record ConsentSessionDecision(
        UiCapability Capabilities,
        Lazy<Task<SessionConsentDecision>> Decision);
}

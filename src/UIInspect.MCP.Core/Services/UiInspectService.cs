// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;

namespace UIInspect.MCP.Core.Services;

/// <summary>Coordinates consent, process identity, rate limits, audit, and UI Automation sessions.</summary>
public sealed class UiInspectService : IAsyncDisposable
{
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly IAuditSink _auditSink;
    private readonly IUiAutomationBackend _backend;
    private readonly ConsentRegistry _consentRegistry;
    private readonly IUserConsentPrompt _consentPrompt;
    private readonly UiInspectOptions _options;
    private readonly IProcessIdentityProvider _processes;
    private readonly IOperationRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="UiInspectService"/> class.</summary>
    public UiInspectService(
        IUiAutomationBackend backend,
        IProcessIdentityProvider processes,
        IUserConsentPrompt consentPrompt,
        ConsentRegistry consentRegistry,
        IOperationRateLimiter rateLimiter,
        IAuditSink auditSink,
        TimeProvider timeProvider,
        UiInspectOptions options)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _consentPrompt = consentPrompt ?? throw new ArgumentNullException(nameof(consentPrompt));
        _consentRegistry = consentRegistry ?? throw new ArgumentNullException(nameof(consentRegistry));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(options);
    }

    /// <summary>List top-level windows in the current desktop session.</summary>
    public async ValueTask<UiResult<IReadOnlyList<WindowDescriptor>>> DiscoverAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var clientHash = GetClientHash(clientId);
        var limited = CheckRate($"discover:{clientHash}", _options.DiscoveryRatePerMinute);
        if (limited is not null)
        {
            await AuditAsync("discovery", "denied", clientHash, null, "discover", "rate_limited", cancellationToken);
            return UiResult<IReadOnlyList<WindowDescriptor>>.Fail(
                "rate_limited",
                "Window discovery rate limit exceeded.",
                limited);
        }

        var windows = await _backend.ListTopLevelWindowsAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync("discovery", "completed", clientHash, null, "discover", null, cancellationToken);
        return UiResult<IReadOnlyList<WindowDescriptor>>.Ok(windows);
    }

    /// <summary>Request explicit local-user consent for one process instance.</summary>
    public async ValueTask<UiResult<ConsentGrantInfo>> RequestConsentAsync(
        int processId,
        bool allowActions,
        bool allowKeyboard,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var clientHash = GetClientHash(clientId);
        var target = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            await AuditAsync("consent_requested", "denied", clientHash, null, "request_consent", "target_unavailable", cancellationToken);
            return UiResult<ConsentGrantInfo>.Fail("target_unavailable", "The target process is unavailable or inaccessible.");
        }

        var limited = CheckRate(
            $"consent:{clientHash}:{target.ProcessId}:{target.StartedAtUtc.UtcTicks}",
            _options.ConsentPromptRatePerMinute);
        if (limited is not null)
        {
            await AuditAsync("consent_requested", "denied", clientHash, target, "request_consent", "rate_limited", cancellationToken);
            return UiResult<ConsentGrantInfo>.Fail("rate_limited", "Consent prompt rate limit exceeded.", limited);
        }

        var capabilities = UiCapability.Inspect;
        if (allowActions)
        {
            capabilities |= UiCapability.Interact;
        }

        if (allowKeyboard)
        {
            capabilities |= UiCapability.Keyboard;
        }

        await AuditAsync("consent_requested", "pending", clientHash, target, "request_consent", null, cancellationToken);
        var approved = await _consentPrompt.RequestAsync(target, capabilities, clientId, cancellationToken).ConfigureAwait(false);
        if (!approved)
        {
            await AuditAsync("consent_denied", "denied", clientHash, target, "request_consent", "user_denied", cancellationToken);
            return UiResult<ConsentGrantInfo>.Fail("consent_denied", "The local user denied access to the target process.");
        }

        var revalidated = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (revalidated != target)
        {
            await AuditAsync("consent_denied", "denied", clientHash, target, "request_consent", "target_changed", cancellationToken);
            return UiResult<ConsentGrantInfo>.Fail("target_changed", "The target process changed while consent was requested.");
        }

        var grant = _consentRegistry.Grant(clientHash, target, capabilities, _options.ConsentDuration);
        await AuditAsync("consent_granted", "allowed", clientHash, target, "request_consent", null, cancellationToken);
        return UiResult<ConsentGrantInfo>.Ok(
            new ConsentGrantInfo(grant.Id, grant.Target, grant.Capabilities, grant.ExpiresAtUtc));
    }

    /// <summary>Attach a consent-gated session by process ID and optional native window handle.</summary>
    public async ValueTask<UiResult<AutomationSessionInfo>> AttachAsync(
        int processId,
        long? windowHandle,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var clientHash = GetClientHash(clientId);
        var target = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return UiResult<AutomationSessionInfo>.Fail("target_unavailable", "The target process is unavailable or inaccessible.");
        }

        var grant = _consentRegistry.FindActive(clientHash, target, UiCapability.Inspect);
        if (grant is null)
        {
            await AuditAsync("attach", "denied", clientHash, target, "attach", "consent_required", cancellationToken);
            return UiResult<AutomationSessionInfo>.Fail("consent_required", "Request local-user consent for this process instance before attaching.");
        }

        var platformSession = await _backend.AttachAsync(target, windowHandle, cancellationToken).ConfigureAwait(false);
        var revalidated = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (revalidated != target || platformSession.Target != target)
        {
            await platformSession.DisposeAsync().ConfigureAwait(false);
            _ = _consentRegistry.RevokeTarget(target);
            await AuditAsync("attach", "failed", clientHash, target, "attach", "target_changed", cancellationToken);
            return UiResult<AutomationSessionInfo>.Fail("target_changed", "The target process changed while the session was attached.");
        }

        var sessionId = $"s_{Guid.NewGuid():N}";
        var entry = new SessionEntry(clientHash, grant, platformSession);
        _sessions[sessionId] = entry;

        await AuditAsync("attach", "completed", clientHash, target, "attach", null, cancellationToken);
        return UiResult<AutomationSessionInfo>.Ok(
            new AutomationSessionInfo(sessionId, target, platformSession.WindowHandle, grant.ExpiresAtUtc));
    }

    /// <summary>Inspect the semantic UI tree for an attached session.</summary>
    public async ValueTask<UiResult<UiTreeSnapshot>> InspectAsync(
        string sessionId,
        int maxDepth,
        int maxNodes,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        if (maxDepth < 0 || maxDepth > _options.MaximumTreeDepth)
        {
            return UiResult<UiTreeSnapshot>.Fail(
                "invalid_depth",
                $"maxDepth must be between 0 and {_options.MaximumTreeDepth}.");
        }

        if (maxNodes < 1 || maxNodes > _options.MaximumTreeNodes)
        {
            return UiResult<UiTreeSnapshot>.Fail(
                "invalid_node_limit",
                $"maxNodes must be between 1 and {_options.MaximumTreeNodes}.");
        }

        var authorization = await AuthorizeSessionAsync(
            sessionId,
            clientId,
            UiCapability.Inspect,
            "inspect",
            _options.InspectionRatePerMinute,
            cancellationToken).ConfigureAwait(false);
        if (!authorization.Success)
        {
            return UiResult<UiTreeSnapshot>.Fail(
                authorization.Error!.Code,
                authorization.Error.Message,
                authorization.Error.RetryAfterMilliseconds is double milliseconds
                    ? TimeSpan.FromMilliseconds(milliseconds)
                    : null);
        }

        var entry = authorization.Data!;
        var snapshot = await entry.Session.InspectAsync(sessionId, maxDepth, maxNodes, cancellationToken).ConfigureAwait(false);
        await AuditAsync("inspection", "completed", entry.ClientHash, entry.Session.Target, "inspect", null, cancellationToken);
        return UiResult<UiTreeSnapshot>.Ok(snapshot);
    }

    /// <summary>Invoke an element through UI Automation InvokePattern.</summary>
    public ValueTask<UiResult<ActionReceipt>> InvokeAsync(
        string sessionId,
        string elementReference,
        string clientId,
        CancellationToken cancellationToken = default) =>
        ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Interact,
            "invoke",
            static (session, reference, token) => session.InvokeAsync(reference, token),
            cancellationToken);

    /// <summary>Click the center of a semantically resolved element.</summary>
    public ValueTask<UiResult<ActionReceipt>> ClickAsync(
        string sessionId,
        string elementReference,
        string clientId,
        CancellationToken cancellationToken = default) =>
        ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Interact,
            "click",
            static (session, reference, token) => session.ClickAsync(reference, token),
            cancellationToken);

    /// <summary>Set text/value through UI Automation ValuePattern.</summary>
    public ValueTask<UiResult<ActionReceipt>> SetValueAsync(
        string sessionId,
        string elementReference,
        string value,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Interact,
            "set_value",
            (session, reference, token) => session.SetValueAsync(reference, value, token),
            cancellationToken);
    }

    /// <summary>Select an item through UI Automation SelectionItemPattern.</summary>
    public ValueTask<UiResult<ActionReceipt>> SelectAsync(
        string sessionId,
        string elementReference,
        string clientId,
        CancellationToken cancellationToken = default) =>
        ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Interact,
            "select",
            static (session, reference, token) => session.SelectAsync(reference, token),
            cancellationToken);

    /// <summary>Expand or collapse an element through UI Automation ExpandCollapsePattern.</summary>
    public ValueTask<UiResult<ActionReceipt>> SetExpandedAsync(
        string sessionId,
        string elementReference,
        bool expand,
        string clientId,
        CancellationToken cancellationToken = default) =>
        ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Interact,
            expand ? "expand" : "collapse",
            (session, reference, token) => session.SetExpandedAsync(reference, expand, token),
            cancellationToken);

    /// <summary>Focus an element and send one allowlisted logical key.</summary>
    public ValueTask<UiResult<ActionReceipt>> SendKeyAsync(
        string sessionId,
        string elementReference,
        string key,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Keyboard,
            "send_key",
            (session, reference, token) => session.SendKeyAsync(reference, key, token),
            cancellationToken);
    }

    /// <summary>Close and dispose an attached session.</summary>
    public async ValueTask<UiResult<bool>> CloseSessionAsync(
        string sessionId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var clientHash = GetClientHash(clientId);
        if (!_sessions.TryGetValue(sessionId, out var entry) || entry.ClientHash != clientHash)
        {
            return UiResult<bool>.Fail("session_not_found", "The session does not exist for this client.");
        }

        _ = _sessions.TryRemove(sessionId, out _);
        await entry.Session.DisposeAsync().ConfigureAwait(false);
        await AuditAsync("session_closed", "completed", clientHash, entry.Session.Target, "close_session", null, cancellationToken);
        return UiResult<bool>.Ok(true);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        var sessions = _sessions.ToArray();
        _sessions.Clear();
        foreach (var pair in sessions)
        {
            await pair.Value.Session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void ValidateOptions(UiInspectOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ConsentDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumTreeDepth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumTreeNodes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DiscoveryRatePerMinute, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.InspectionRatePerMinute, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ActionRatePerMinute, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ConsentPromptRatePerMinute, 1);
    }

    private async ValueTask<UiResult<ActionReceipt>> ExecuteActionAsync(
        string sessionId,
        string elementReference,
        string clientId,
        UiCapability capability,
        string action,
        Func<IUiAutomationSession, string, CancellationToken, ValueTask<PlatformActionResult>> execute,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementReference);
        var authorization = await AuthorizeSessionAsync(
            sessionId,
            clientId,
            capability,
            action,
            _options.ActionRatePerMinute,
            cancellationToken).ConfigureAwait(false);
        if (!authorization.Success)
        {
            return UiResult<ActionReceipt>.Fail(
                authorization.Error!.Code,
                authorization.Error.Message,
                authorization.Error.RetryAfterMilliseconds is double milliseconds
                    ? TimeSpan.FromMilliseconds(milliseconds)
                    : null);
        }

        var entry = authorization.Data!;
        var result = await execute(entry.Session, elementReference, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await AuditAsync("action", "failed", entry.ClientHash, entry.Session.Target, action, result.ErrorCode, cancellationToken);
            return UiResult<ActionReceipt>.Fail(
                result.ErrorCode ?? "automation_failed",
                result.Message);
        }

        await AuditAsync("action", "completed", entry.ClientHash, entry.Session.Target, action, null, cancellationToken);
        return UiResult<ActionReceipt>.Ok(
            new ActionReceipt(action, elementReference, true, result.Message));
    }

    private async ValueTask<UiResult<SessionEntry>> AuthorizeSessionAsync(
        string sessionId,
        string clientId,
        UiCapability capability,
        string operation,
        int permits,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var clientHash = GetClientHash(clientId);
        if (!_sessions.TryGetValue(sessionId, out var entry) || entry.ClientHash != clientHash)
        {
            return UiResult<SessionEntry>.Fail("session_not_found", "The session does not exist for this client.");
        }

        var current = await _processes.ResolveAsync(entry.Session.Target.ProcessId, cancellationToken).ConfigureAwait(false);
        if (current != entry.Session.Target)
        {
            _ = _sessions.TryRemove(sessionId, out _);
            _ = _consentRegistry.RevokeTarget(entry.Session.Target);
            await entry.Session.DisposeAsync().ConfigureAwait(false);
            await AuditAsync("session", "failed", clientHash, entry.Session.Target, operation, "target_changed", cancellationToken);
            return UiResult<SessionEntry>.Fail("target_changed", "The target process exited or changed.");
        }

        var grant = _consentRegistry.FindActive(clientHash, current, capability);
        if (grant is null || grant.Id != entry.Grant.Id)
        {
            return UiResult<SessionEntry>.Fail("consent_expired", "The required consent is absent, expired, or revoked.");
        }

        var limited = CheckRate(
            $"{operation}:{clientHash}:{current.ProcessId}:{current.StartedAtUtc.UtcTicks}",
            permits);
        return limited is null
            ? UiResult<SessionEntry>.Ok(entry)
            : UiResult<SessionEntry>.Fail("rate_limited", $"{operation} rate limit exceeded.", limited);
    }

    private static string GetClientHash(string clientId) => AuditHash.Compute(clientId);

    private TimeSpan? CheckRate(string bucket, int permits)
    {
        var decision = _rateLimiter.TryAcquire(bucket, permits, RateWindow);
        return decision.IsAllowed ? null : decision.RetryAfter;
    }

    private ValueTask AuditAsync(
        string eventType,
        string outcome,
        string clientHash,
        ProcessIdentity? target,
        string operation,
        string? reason,
        CancellationToken cancellationToken) =>
        _auditSink.WriteAsync(
            new AuditEvent(
                _timeProvider.GetUtcNow(),
                Guid.NewGuid().ToString("N"),
                eventType,
                outcome,
                clientHash,
                target?.ProcessId,
                target?.StartedAtUtc,
                operation,
                reason),
            cancellationToken);

    private sealed record SessionEntry(
        string ClientHash,
        ConsentGrant Grant,
        IUiAutomationSession Session);
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;

namespace UIInspect.MCP.Core.Services;

/// <summary>Coordinates consent, process identity, rate limits, audit, and UI Automation sessions.</summary>
public sealed class UiInspectService : IAsyncDisposable
{
    /// <summary>Outcome written when an operation is rejected.</summary>
    private const string DeniedOutcome = "denied";

    /// <summary>Outcome written when an operation completes.</summary>
    private const string CompletedOutcome = "completed";

    /// <summary>Outcome written when consent is granted.</summary>
    private const string AllowedOutcome = "allowed";

    /// <summary>Outcome written when an operation fails.</summary>
    private const string FailedOutcome = "failed";

    /// <summary>Outcome written while a consent prompt is active.</summary>
    private const string PendingOutcome = "pending";

    /// <summary>Rate-limit error code.</summary>
    private const string RateLimitedCode = "rate_limited";

    /// <summary>Unavailable-target error code.</summary>
    private const string TargetUnavailableCode = "target_unavailable";

    /// <summary>Target-changed error code.</summary>
    private const string TargetChangedCode = "target_changed";

    /// <summary>User-denied-consent error code.</summary>
    private const string ConsentDeniedCode = "consent_denied";

    /// <summary>Missing-session error code.</summary>
    private const string SessionNotFoundCode = "session_not_found";

    /// <summary>Missing-session error message.</summary>
    private const string SessionNotFoundMessage = "The session does not exist for this client.";

    /// <summary>Discovery operation name.</summary>
    private const string DiscoverOperation = "discover";

    /// <summary>Consent request operation name.</summary>
    private const string RequestConsentOperation = "request_consent";

    /// <summary>Attach operation name.</summary>
    private const string AttachOperation = "attach";

    /// <summary>Inspection operation name.</summary>
    private const string InspectOperation = "inspect";

    /// <summary>Session audit operation name.</summary>
    private const string SessionOperation = "session";

    /// <summary>Close-session operation name.</summary>
    private const string CloseSessionOperation = "close_session";

    /// <summary>Session-closed audit event type.</summary>
    private const string SessionClosedEvent = "session_closed";

    /// <summary>Consent-requested audit event type.</summary>
    private const string ConsentRequestedEvent = "consent_requested";

    /// <summary>Consent-denied audit event type.</summary>
    private const string ConsentDeniedEvent = "consent_denied";

    /// <summary>Time range used by all local fixed-window rate limits.</summary>
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    /// <summary>Stores active sessions keyed by their opaque identifiers.</summary>
    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);

    /// <summary>Writes redacted audit records.</summary>
    private readonly IAuditSink _auditSink;

    /// <summary>Creates and operates platform UI Automation sessions.</summary>
    private readonly IUiAutomationBackend _backend;

    /// <summary>Stores short-lived grants.</summary>
    private readonly ConsentRegistry _consentRegistry;

    /// <summary>Validates optional user-session unattended approval leases.</summary>
    private readonly IUnattendedApprovalAuthorizer _unattendedApprovals;

    /// <summary>Obtains trusted local-user approval.</summary>
    private readonly ISessionUserConsentPrompt _consentPrompt;

    /// <summary>Provides response bounds and safety limits.</summary>
    private readonly UiInspectOptions _options;

    /// <summary>Resolves target process instances.</summary>
    private readonly IProcessIdentityProvider _processes;

    /// <summary>Enforces per-operation rate limits.</summary>
    private readonly IOperationRateLimiter _rateLimiter;

    /// <summary>Provides the current UTC time.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="UiInspectService"/> class.</summary>
    /// <param name="dependencies">Collaborating service dependencies.</param>
    /// <param name="options">Safety and response bound options.</param>
    public UiInspectService(UiInspectServiceDependencies dependencies, UiInspectOptions options)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _backend = dependencies.Backend ?? throw new ArgumentNullException(nameof(dependencies.Backend));
        _processes = dependencies.Processes ?? throw new ArgumentNullException(nameof(dependencies.Processes));
        _consentPrompt = dependencies.ConsentPrompt ?? throw new ArgumentNullException(nameof(dependencies.ConsentPrompt));
        _consentRegistry = dependencies.ConsentRegistry ?? throw new ArgumentNullException(nameof(dependencies.ConsentRegistry));
        _unattendedApprovals = dependencies.UnattendedApprovals ?? throw new ArgumentNullException(nameof(dependencies.UnattendedApprovals));
        _rateLimiter = dependencies.RateLimiter ?? throw new ArgumentNullException(nameof(dependencies.RateLimiter));
        _auditSink = dependencies.AuditSink ?? throw new ArgumentNullException(nameof(dependencies.AuditSink));
        _timeProvider = dependencies.TimeProvider ?? throw new ArgumentNullException(nameof(dependencies.TimeProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(options);
    }

    /// <summary>List top-level windows in the current desktop session.</summary>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Visible top-level windows or a safe error.</returns>
    public async ValueTask<UiResult<IReadOnlyList<WindowDescriptor>>> DiscoverAsync(string clientId, CancellationToken cancellationToken)
    {
        var clientHash = GetClientHash(clientId);
        var limited = CheckRate($"{DiscoverOperation}:{clientHash}", _options.DiscoveryRatePerMinute);
        if (limited is not null)
        {
            await AuditAsync("discovery", DeniedOutcome, clientHash, null, DiscoverOperation, RateLimitedCode, cancellationToken).ConfigureAwait(false);
            return UiResult<IReadOnlyList<WindowDescriptor>>.Fail(RateLimitedCode, "Window discovery rate limit exceeded.", limited);
        }

        var windows = await _backend.ListTopLevelWindowsAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync("discovery", CompletedOutcome, clientHash, null, DiscoverOperation, null, cancellationToken).ConfigureAwait(false);
        return UiResult<IReadOnlyList<WindowDescriptor>>.Ok(windows);
    }

    /// <summary>Request explicit local-user consent for one process instance.</summary>
    /// <param name="processId">Target operating-system process identifier.</param>
    /// <param name="allowActions">Whether interaction capability is requested.</param>
    /// <param name="allowKeyboard">Whether logical keyboard capability is requested.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Consent details or a safe error.</returns>
    public async ValueTask<UiResult<ConsentGrantInfo>> RequestConsentAsync(int processId, bool allowActions, bool allowKeyboard, string clientId, CancellationToken cancellationToken)
    {
        var clientHash = GetClientHash(clientId);
        var target = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            await AuditAsync(ConsentRequestedEvent, DeniedOutcome, clientHash, null, RequestConsentOperation, TargetUnavailableCode, cancellationToken).ConfigureAwait(false);
            return UiResult<ConsentGrantInfo>.Fail(TargetUnavailableCode, "The target process is unavailable or inaccessible.");
        }

        var capabilities = BuildCapabilities(allowActions, allowKeyboard);
        var reused = await TryReuseConsentAsync(clientHash, target, capabilities, cancellationToken).ConfigureAwait(false);
        if (reused is not null)
        {
            return reused;
        }

        var unattended = await TryGrantUnattendedConsentAsync(clientHash, target, capabilities, cancellationToken).ConfigureAwait(false);
        if (unattended is not null)
        {
            return unattended;
        }

        await AuditAsync(ConsentRequestedEvent, PendingOutcome, clientHash, target, RequestConsentOperation, null, cancellationToken).ConfigureAwait(false);
        var decision = await _consentPrompt.RequestAsync(target, capabilities, clientId, cancellationToken).ConfigureAwait(false);
        if (decision.RetryAfter is { } retryAfter)
        {
            await AuditAsync(ConsentRequestedEvent, DeniedOutcome, clientHash, target, RequestConsentOperation, RateLimitedCode, cancellationToken).ConfigureAwait(false);
            return UiResult<ConsentGrantInfo>.Fail(RateLimitedCode, "Consent prompt rate limit exceeded.", retryAfter);
        }

        if (!decision.IsApproved)
        {
            await AuditAsync(ConsentDeniedEvent, DeniedOutcome, clientHash, target, RequestConsentOperation, "user_or_session_policy_denied", cancellationToken).ConfigureAwait(false);
            return UiResult<ConsentGrantInfo>.Fail(
                ConsentDeniedCode,
                "The consent request was denied or exceeds the capabilities approved for this server session.");
        }

        var revalidated = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (revalidated != target)
        {
            await AuditAsync(ConsentDeniedEvent, DeniedOutcome, clientHash, target, RequestConsentOperation, TargetChangedCode, cancellationToken).ConfigureAwait(false);
            return UiResult<ConsentGrantInfo>.Fail(TargetChangedCode, "The target process changed while consent was requested.");
        }

        var grant = _consentRegistry.GrantOrGetActive(clientHash, target, capabilities, _options.ConsentDuration);
        await AuditAsync("consent_granted", AllowedOutcome, clientHash, target, RequestConsentOperation, null, cancellationToken).ConfigureAwait(false);
        return UiResult<ConsentGrantInfo>.Ok(
            new(grant.Id, grant.Target, grant.Capabilities, grant.ExpiresAtUtc, grant.Origin));
    }

    /// <summary>Attach a consent-gated session by process ID and optional native window handle.</summary>
    /// <param name="processId">Target operating-system process identifier.</param>
    /// <param name="windowHandle">Optional top-level native window handle.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attached session information or a safe error.</returns>
    public async ValueTask<UiResult<AutomationSessionInfo>> AttachAsync(int processId, long? windowHandle, string clientId, CancellationToken cancellationToken)
    {
        var clientHash = GetClientHash(clientId);
        var target = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            await AuditAsync(AttachOperation, DeniedOutcome, clientHash, null, AttachOperation, TargetUnavailableCode, cancellationToken).ConfigureAwait(false);
            return UiResult<AutomationSessionInfo>.Fail(TargetUnavailableCode, "The target process is unavailable or inaccessible.");
        }

        var grant = _consentRegistry.FindActive(clientHash, target, UiCapability.Inspect);
        if (grant is null
            || !await IsGrantAuthorityActiveAsync(grant, UiCapability.Inspect, cancellationToken).ConfigureAwait(false))
        {
            if (grant is not null)
            {
                _ = _consentRegistry.Revoke(grant.Id);
            }

            await AuditAsync(AttachOperation, DeniedOutcome, clientHash, target, AttachOperation, "consent_required", cancellationToken).ConfigureAwait(false);
            return UiResult<AutomationSessionInfo>.Fail("consent_required", "Request local-user consent for this process instance before attaching.");
        }

        var platformSession = await _backend.AttachAsync(target, windowHandle, cancellationToken).ConfigureAwait(false);
        var revalidated = await _processes.ResolveAsync(processId, cancellationToken).ConfigureAwait(false);
        if (revalidated != target || platformSession.Target != target)
        {
            await platformSession.DisposeAsync().ConfigureAwait(false);
            _ = _consentRegistry.RevokeTarget(target);
            await AuditAsync(AttachOperation, FailedOutcome, clientHash, target, AttachOperation, TargetChangedCode, cancellationToken).ConfigureAwait(false);
            return UiResult<AutomationSessionInfo>.Fail(TargetChangedCode, "The target process changed while the session was attached.");
        }

        var sessionId = $"s_{Guid.NewGuid():N}";
        var entry = new SessionEntry(clientHash, grant, platformSession);
        _sessions[sessionId] = entry;

        await AuditAsync(AttachOperation, CompletedOutcome, clientHash, target, AttachOperation, null, cancellationToken).ConfigureAwait(false);
        return UiResult<AutomationSessionInfo>.Ok(new(sessionId, target, platformSession.WindowHandle, grant.ExpiresAtUtc));
    }

    /// <summary>Inspect the semantic UI tree for an attached session.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="maxDepth">Maximum descendant depth.</param>
    /// <param name="maxNodes">Maximum flattened node count.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Bounded UI tree snapshot or a safe error.</returns>
    public async ValueTask<UiResult<UiTreeSnapshot>> InspectAsync(string sessionId, int maxDepth, int maxNodes, string clientId, CancellationToken cancellationToken)
    {
        if (maxDepth < 0 || maxDepth > _options.MaximumTreeDepth)
        {
            return UiResult<UiTreeSnapshot>.Fail("invalid_depth", $"maxDepth must be between 0 and {_options.MaximumTreeDepth}.");
        }

        if (maxNodes < 1 || maxNodes > _options.MaximumTreeNodes)
        {
            return UiResult<UiTreeSnapshot>.Fail("invalid_node_limit", $"maxNodes must be between 1 and {_options.MaximumTreeNodes}.");
        }

        var authorization = await AuthorizeSessionAsync(sessionId, clientId, UiCapability.Inspect, InspectOperation, _options.InspectionRatePerMinute, cancellationToken).ConfigureAwait(false);
        if (!authorization.Success)
        {
            return UiResult<UiTreeSnapshot>.Fail(authorization.Error!.Code, authorization.Error.Message, ToRetryAfter(authorization.Error));
        }

        var entry = authorization.Data!;
        var snapshot = await entry.Session.InspectAsync(sessionId, maxDepth, maxNodes, cancellationToken).ConfigureAwait(false);
        await AuditAsync("inspection", CompletedOutcome, entry.ClientHash, entry.Session.Target, InspectOperation, null, cancellationToken).ConfigureAwait(false);
        return UiResult<UiTreeSnapshot>.Ok(snapshot);
    }

    /// <summary>Invoke an element through UI Automation InvokePattern.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action receipt or a safe error.</returns>
    public ValueTask<UiResult<ActionReceipt>> InvokeAsync(string sessionId, string elementReference, string clientId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(sessionId, elementReference, clientId, UiCapability.Interact, "invoke", static (session, reference, token) => session.InvokeAsync(reference, token), cancellationToken);

    /// <summary>Click the center of a semantically resolved element.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action receipt or a safe error.</returns>
    public ValueTask<UiResult<ActionReceipt>> ClickAsync(string sessionId, string elementReference, string clientId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(sessionId, elementReference, clientId, UiCapability.Interact, "click", static (session, reference, token) => session.ClickAsync(reference, token), cancellationToken);

    /// <summary>Set text or a value through UI Automation ValuePattern.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="value">Value to assign.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action receipt or a safe error.</returns>
    public ValueTask<UiResult<ActionReceipt>> SetValueAsync(string sessionId, string elementReference, string value, string clientId, CancellationToken cancellationToken)
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
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action receipt or a safe error.</returns>
    public ValueTask<UiResult<ActionReceipt>> SelectAsync(string sessionId, string elementReference, string clientId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Interact,
            "select",
            static (session, reference, token) => session.SelectAsync(reference, token),
            cancellationToken);

    /// <summary>Expand or collapse an element through UI Automation ExpandCollapsePattern.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="expand">Whether to expand the element.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action receipt or a safe error.</returns>
    public ValueTask<UiResult<ActionReceipt>> SetExpandedAsync(string sessionId, string elementReference, bool expand, string clientId, CancellationToken cancellationToken) =>
        ExecuteActionAsync(
            sessionId,
            elementReference,
            clientId,
            UiCapability.Interact,
            expand ? "expand" : "collapse",
            (session, reference, token) => session.SetExpandedAsync(reference, expand, token),
            cancellationToken);

    /// <summary>Focus an element and send one allowlisted logical key.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="key">Logical key.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action receipt or a safe error.</returns>
    public ValueTask<UiResult<ActionReceipt>> SendKeyAsync(string sessionId, string elementReference, string key, string clientId, CancellationToken cancellationToken)
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
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the session was closed, otherwise a safe error.</returns>
    public async ValueTask<UiResult<bool>> CloseSessionAsync(string sessionId, string clientId, CancellationToken cancellationToken)
    {
        var clientHash = GetClientHash(clientId);
        if (!_sessions.TryGetValue(sessionId, out var entry) || !AuditHash.Matches(entry.ClientHash, clientHash))
        {
            await AuditAsync(SessionClosedEvent, DeniedOutcome, clientHash, null, CloseSessionOperation, SessionNotFoundCode, cancellationToken).ConfigureAwait(false);
            return UiResult<bool>.Fail(SessionNotFoundCode, SessionNotFoundMessage);
        }

        if (!_sessions.TryRemove(sessionId, out var removed))
        {
            await AuditAsync(SessionClosedEvent, DeniedOutcome, clientHash, null, CloseSessionOperation, SessionNotFoundCode, cancellationToken).ConfigureAwait(false);
            return UiResult<bool>.Fail(SessionNotFoundCode, SessionNotFoundMessage);
        }

        await removed.Session.DisposeAsync().ConfigureAwait(false);
        await AuditAsync(SessionClosedEvent, CompletedOutcome, clientHash, removed.Session.Target, CloseSessionOperation, null, cancellationToken).ConfigureAwait(false);
        return UiResult<bool>.Ok(true);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var pair in _sessions)
        {
            if (_sessions.TryRemove(pair.Key, out var removed))
            {
                await removed.Session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Hash a client identifier for secure comparison and storage.</summary>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <returns>Stable non-secret hash.</returns>
    private static string GetClientHash(string clientId) => AuditHash.Compute(clientId);

    /// <summary>Build the requested capability ceiling.</summary>
    /// <param name="allowActions">Whether semantic interaction is requested.</param>
    /// <param name="allowKeyboard">Whether logical keyboard input is requested.</param>
    /// <returns>Requested capabilities.</returns>
    private static UiCapability BuildCapabilities(bool allowActions, bool allowKeyboard)
    {
        var capabilities = UiCapability.Inspect;
        if (allowActions)
        {
            capabilities |= UiCapability.Interact;
        }

        if (allowKeyboard)
        {
            capabilities |= UiCapability.Keyboard;
        }

        return capabilities;
    }

    /// <summary>Create the public, redacted view of a local grant.</summary>
    /// <param name="grant">Local grant.</param>
    /// <returns>Public grant details.</returns>
    private static ConsentGrantInfo ToGrantInfo(ConsentGrant grant) =>
        new(grant.Id, grant.Target, grant.Capabilities, grant.ExpiresAtUtc, grant.Origin);

    /// <summary>Validates immutable server safety limits.</summary>
    /// <param name="options">Options to validate.</param>
    private static void ValidateOptions(UiInspectOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ConsentDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ConsentPromptTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumTreeDepth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumTreeNodes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.DiscoveryRatePerMinute, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.InspectionRatePerMinute, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ActionRatePerMinute, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ConsentPromptRatePerMinute, 1);
    }

    /// <summary>Converts a safe error's retry delay to a nullable time span.</summary>
    /// <param name="error">Safe error value.</param>
    /// <returns>Retry delay, when supplied.</returns>
    private static TimeSpan? ToRetryAfter(UiError error) =>
        error.RetryAfterMilliseconds is double milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;

    /// <summary>Reuse one active locally cached grant after validating its authority.</summary>
    /// <param name="clientHash">Hashed client identity.</param>
    /// <param name="target">Exact process identity.</param>
    /// <param name="capabilities">Required capabilities.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A successful result, or <see langword="null"/> when no reusable grant exists.</returns>
    private async ValueTask<UiResult<ConsentGrantInfo>?> TryReuseConsentAsync(
        string clientHash,
        ProcessIdentity target,
        UiCapability capabilities,
        CancellationToken cancellationToken)
    {
        var activeGrant = _consentRegistry.FindActive(clientHash, target, capabilities);
        if (activeGrant is null)
        {
            return null;
        }

        if (!await IsGrantAuthorityActiveAsync(activeGrant, capabilities, cancellationToken).ConfigureAwait(false))
        {
            _ = _consentRegistry.Revoke(activeGrant.Id);
            return null;
        }

        await AuditAsync("consent_reused", AllowedOutcome, clientHash, target, RequestConsentOperation, null, cancellationToken).ConfigureAwait(false);
        return UiResult<ConsentGrantInfo>.Ok(ToGrantInfo(activeGrant));
    }

    /// <summary>Exchange an active broker lease for one exact-process local grant.</summary>
    /// <param name="clientHash">Hashed client identity.</param>
    /// <param name="target">Exact process identity.</param>
    /// <param name="capabilities">Required capabilities.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A grant result, or <see langword="null"/> when no unattended lease exists.</returns>
    private async ValueTask<UiResult<ConsentGrantInfo>?> TryGrantUnattendedConsentAsync(
        string clientHash,
        ProcessIdentity target,
        UiCapability capabilities,
        CancellationToken cancellationToken)
    {
        var lease = await _unattendedApprovals
            .GetActiveLeaseAsync(capabilities, cancellationToken)
            .ConfigureAwait(false);
        if (lease is null || lease.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            return null;
        }

        var current = await _processes.ResolveAsync(target.ProcessId, cancellationToken).ConfigureAwait(false);
        var valid = current == target
            && await _unattendedApprovals
                .ValidateAsync(lease.LeaseId, target, capabilities, cancellationToken)
                .ConfigureAwait(false);
        if (!valid)
        {
            await AuditAsync(ConsentDeniedEvent, DeniedOutcome, clientHash, target, RequestConsentOperation, TargetChangedCode, cancellationToken).ConfigureAwait(false);
            return UiResult<ConsentGrantInfo>.Fail(
                TargetChangedCode,
                "The target process or unattended approval changed while consent was requested.");
        }

        ConsentGrant grant;
        try
        {
            grant = _consentRegistry.GrantOrGetActiveUntil(
                clientHash,
                target,
                capabilities,
                lease.ExpiresAtUtc,
                ConsentOrigin.UnattendedApprovalLease,
                lease.LeaseId);
        }
        catch (ArgumentOutOfRangeException) when (lease.ExpiresAtUtc <= _timeProvider.GetUtcNow())
        {
            return null;
        }

        await AuditAsync("consent_granted", AllowedOutcome, clientHash, target, RequestConsentOperation, "unattended_approval", cancellationToken).ConfigureAwait(false);
        return UiResult<ConsentGrantInfo>.Ok(ToGrantInfo(grant));
    }

    /// <summary>Executes a consent-gated semantic action.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="elementReference">Opaque UI element reference.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="capability">Required consent capability.</param>
    /// <param name="action">Action operation name.</param>
    /// <param name="execute">Platform action delegate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Action receipt or a safe error.</returns>
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
        var authorization = await AuthorizeSessionAsync(sessionId, clientId, capability, action, _options.ActionRatePerMinute, cancellationToken).ConfigureAwait(false);
        if (!authorization.Success)
        {
            return UiResult<ActionReceipt>.Fail(authorization.Error!.Code, authorization.Error.Message, ToRetryAfter(authorization.Error));
        }

        var entry = authorization.Data!;
        var result = await execute(entry.Session, elementReference, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await AuditAsync("action", FailedOutcome, entry.ClientHash, entry.Session.Target, action, result.ErrorCode, cancellationToken).ConfigureAwait(false);
            return UiResult<ActionReceipt>.Fail(result.ErrorCode ?? "automation_failed", result.Message);
        }

        await AuditAsync("action", CompletedOutcome, entry.ClientHash, entry.Session.Target, action, null, cancellationToken).ConfigureAwait(false);
        return UiResult<ActionReceipt>.Ok(new(action, elementReference, true, result.Message));
    }

    /// <summary>Verifies session ownership, consent, target identity, and rate limits.</summary>
    /// <param name="sessionId">Opaque session identifier.</param>
    /// <param name="clientId">Untrusted client identifier.</param>
    /// <param name="capability">Required consent capability.</param>
    /// <param name="operation">Operation name.</param>
    /// <param name="permits">Permitted operations per rate window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authorized entry or a safe error.</returns>
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
        if (!_sessions.TryGetValue(sessionId, out var entry) || !AuditHash.Matches(entry.ClientHash, clientHash))
        {
            return UiResult<SessionEntry>.Fail(SessionNotFoundCode, "The session does not exist for this client.");
        }

        var current = await _processes.ResolveAsync(entry.Session.Target.ProcessId, cancellationToken).ConfigureAwait(false);
        if (current != entry.Session.Target)
        {
            _ = _consentRegistry.RevokeTarget(entry.Session.Target);
            if (_sessions.TryRemove(sessionId, out var removed))
            {
                await removed.Session.DisposeAsync().ConfigureAwait(false);
            }

            await AuditAsync(SessionOperation, FailedOutcome, clientHash, entry.Session.Target, operation, TargetChangedCode, cancellationToken).ConfigureAwait(false);
            return UiResult<SessionEntry>.Fail(TargetChangedCode, "The target process exited or changed.");
        }

        var grant = _consentRegistry.FindActive(clientHash, current, capability);
        if (grant is null
            || grant.Id != entry.Grant.Id
            || !await IsGrantAuthorityActiveAsync(grant, capability, cancellationToken).ConfigureAwait(false))
        {
            if (_sessions.TryRemove(sessionId, out var removed))
            {
                await removed.Session.DisposeAsync().ConfigureAwait(false);
            }

            await AuditAsync(SessionOperation, DeniedOutcome, clientHash, entry.Session.Target, operation, "consent_expired", cancellationToken).ConfigureAwait(false);
            return UiResult<SessionEntry>.Fail("consent_expired", "The required consent is absent, expired, or revoked.");
        }

        var limited = CheckRate($"{operation}:{clientHash}:{current.ProcessId}:{current.StartedAtUtc.UtcTicks}", permits);
        if (limited is null)
        {
            return UiResult<SessionEntry>.Ok(entry);
        }

        await AuditAsync(SessionOperation, DeniedOutcome, clientHash, entry.Session.Target, operation, RateLimitedCode, cancellationToken).ConfigureAwait(false);
        return UiResult<SessionEntry>.Fail(RateLimitedCode, $"{operation} rate limit exceeded.", limited);
    }

    /// <summary>Validate any external authority associated with a local grant.</summary>
    /// <param name="grant">Local grant.</param>
    /// <param name="requiredCapabilities">Capabilities required by the operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> while the grant authority remains active.</returns>
    private ValueTask<bool> IsGrantAuthorityActiveAsync(
        ConsentGrant grant,
        UiCapability requiredCapabilities,
        CancellationToken cancellationToken)
    {
        if (grant.Origin == ConsentOrigin.ExplicitWindowsPrompt)
        {
            return ValueTask.FromResult(true);
        }

        return grant.AuthorityId is Guid authorityId
            ? _unattendedApprovals.ValidateAsync(authorityId, grant.Target, requiredCapabilities, cancellationToken)
            : ValueTask.FromResult(false);
    }

    /// <summary>Obtains a rate-limit retry delay when an operation cannot proceed.</summary>
    /// <param name="bucket">Non-secret rate bucket.</param>
    /// <param name="permits">Maximum permits per time window.</param>
    /// <returns>Retry delay, or null when the operation is permitted.</returns>
    private TimeSpan? CheckRate(string bucket, int permits)
    {
        var decision = _rateLimiter.TryAcquire(bucket, permits, RateWindow);
        return decision.IsAllowed ? null : decision.RetryAfter;
    }

    /// <summary>Writes a redacted audit event.</summary>
    /// <param name="eventType">Audit event type.</param>
    /// <param name="outcome">Audit outcome.</param>
    /// <param name="clientHash">Hashed client identity.</param>
    /// <param name="target">Optional target process.</param>
    /// <param name="operation">Operation name.</param>
    /// <param name="reason">Safe reason code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audit write completion.</returns>
    private ValueTask AuditAsync(string eventType, string outcome, string clientHash, ProcessIdentity? target, string operation, string? reason, CancellationToken cancellationToken) =>
        _auditSink.WriteAsync(
            new(
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

    /// <summary>Associates a platform session with its owner and grant.</summary>
    /// <param name="ClientHash">Hashed owner identity.</param>
    /// <param name="Grant">Consent grant used to create the session.</param>
    /// <param name="Session">Platform session.</param>
    private sealed record SessionEntry(string ClientHash, ConsentGrant Grant, IUiAutomationSession Session);
}

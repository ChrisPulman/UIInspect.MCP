// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Security;

namespace UIInspect.MCP.Core.Services;

/// <summary>Collaborators required to coordinate secure UI inspection sessions.</summary>
/// <param name="Backend">Platform UI Automation backend.</param>
/// <param name="Processes">Live process identity resolver.</param>
/// <param name="ConsentPrompt">Trusted local-user consent prompt.</param>
/// <param name="ConsentRegistry">Short-lived consent registry.</param>
/// <param name="RateLimiter">Operation rate limiter.</param>
/// <param name="AuditSink">Redacted audit sink.</param>
/// <param name="TimeProvider">UTC time provider.</param>
public sealed record UiInspectServiceDependencies(
    IUiAutomationBackend Backend,
    IProcessIdentityProvider Processes,
    IUserConsentPrompt ConsentPrompt,
    ConsentRegistry ConsentRegistry,
    IOperationRateLimiter RateLimiter,
    IAuditSink AuditSink,
    TimeProvider TimeProvider);

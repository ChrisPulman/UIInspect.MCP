// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using Microsoft.Extensions.DependencyInjection;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Auditing;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Security;
using UIInspect.MCP.Core.Services;
using UIInspect.MCP.Windows.Automation;
using UIInspect.MCP.Windows.Processes;
using UIInspect.MCP.Windows.Security;

namespace UIInspect.MCP.Windows.DependencyInjection;

/// <summary>Registers the secure Windows UIA3 composition.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Add UIInspect core policy and Windows UI Automation services.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="options">Optional safety bounds.</param>
    /// <param name="auditPath">Optional append-only JSONL audit path.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddWindowsUiInspect(
        this IServiceCollection services,
        UiInspectOptions? options = null,
        string? auditPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var resolvedOptions = options ?? new UiInspectOptions();
        var resolvedAuditPath = auditPath ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UIInspect.MCP",
                "audit",
                "actions.jsonl");

        _ = services.AddSingleton(TimeProvider.System);
        _ = services.AddSingleton(resolvedOptions);
        _ = services.AddSingleton<ConsentRegistry>();
        _ = services.AddSingleton<IOperationRateLimiter, FixedWindowRateLimiter>();
        _ = services.AddSingleton<IProcessIdentityProvider, WindowsProcessIdentityProvider>();
        _ = services.AddSingleton<IUserConsentPrompt, TrustedWindowsConsentPrompt>();
        _ = services.AddSingleton<IUiAutomationBackend, FlaUiAutomationBackend>();
        _ = services.AddSingleton<IAuditSink>(_ => new JsonLineAuditSink(resolvedAuditPath));
        _ = services.AddSingleton<UiInspectService>();
        return services;
    }
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    /// <summary>Extension members for registering UIInspect services on a service collection.</summary>
    /// <param name="services">Service collection receiving the registrations.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Add UIInspect core policy and Windows UI Automation services using default safety bounds and audit path.</summary>
        /// <returns>The same service collection.</returns>
        public IServiceCollection AddWindowsUiInspect() => services.AddWindowsUiInspect(null, null);

        /// <summary>Add UIInspect core policy and Windows UI Automation services.</summary>
        /// <param name="options">Safety bounds, or <see langword="null"/> for defaults.</param>
        /// <param name="auditPath">Append-only JSONL audit path, or <see langword="null"/> for the default.</param>
        /// <returns>The same service collection.</returns>
        public IServiceCollection AddWindowsUiInspect(UiInspectOptions? options, string? auditPath)
        {
            ArgumentNullException.ThrowIfNull(services);
            var resolvedOptions = options ?? new UiInspectOptions();
            var resolvedAuditPath = auditPath
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "UIInspect.MCP",
                    "audit",
                    "actions.jsonl");

            _ = services.AddSingleton(TimeProvider.System);
            _ = services.AddSingleton(resolvedOptions);
            _ = services.AddSingleton<ConsentRegistry>();
            _ = services.AddSingleton<IUnattendedApprovalAuthorizer, WindowsUnattendedApprovalAuthorizer>();
            _ = services.AddSingleton<IOperationRateLimiter, FixedWindowRateLimiter>();
            _ = services.AddSingleton<IProcessIdentityProvider, WindowsProcessIdentityProvider>();
            _ = services.AddSingleton<IUserConsentPrompt, TrustedWindowsConsentPrompt>();
            _ = services.AddSingleton<ISessionUserConsentPrompt>(
                static provider => new SessionUserConsentPrompt(
                    provider.GetRequiredService<IUserConsentPrompt>(),
                    provider.GetRequiredService<IOperationRateLimiter>(),
                    provider.GetRequiredService<UiInspectOptions>()));
            _ = services.AddSingleton<IUiAutomationBackend, FlaUiAutomationBackend>();
            _ = services.AddSingleton<IAuditSink>(_ => new JsonLineAuditSink(resolvedAuditPath));
            _ = services.AddSingleton(CreateDependencies);
            _ = services.AddSingleton<UiInspectService>();
            return services;
        }
    }

    /// <summary>Compose the collaborators used by the secure service coordinator.</summary>
    /// <param name="provider">Configured dependency injection provider.</param>
    /// <returns>Resolved coordinator dependencies.</returns>
    private static UiInspectServiceDependencies CreateDependencies(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new(
            provider.GetRequiredService<IUiAutomationBackend>(),
            provider.GetRequiredService<IProcessIdentityProvider>(),
            provider.GetRequiredService<ISessionUserConsentPrompt>(),
            provider.GetRequiredService<ConsentRegistry>(),
            provider.GetRequiredService<IUnattendedApprovalAuthorizer>(),
            provider.GetRequiredService<IOperationRateLimiter>(),
            provider.GetRequiredService<IAuditSink>(),
            provider.GetRequiredService<TimeProvider>());
    }
}

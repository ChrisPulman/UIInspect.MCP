// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Microsoft.Extensions.DependencyInjection;
using UIInspect.MCP.Core.Abstractions;
using UIInspect.MCP.Core.Configuration;
using UIInspect.MCP.Core.Models;
using UIInspect.MCP.Core.Security;

namespace UIInspect.MCP.Tests;

/// <summary>Tests explicit startup authorization and the boundaries retained in unrestricted mode.</summary>
public sealed class UnrestrictedStartupTests
{
    /// <summary>Small bounded inspection size used by the fixture.</summary>
    private const int InspectionNodes = 10;

    /// <summary>Duration much longer than any ordinary approval.</summary>
    private const int LongRunningDays = 365;

    /// <summary>Stable fixture element reference.</summary>
    private const string ElementReference = "e_1_0";

    /// <summary>Command-line authorization selects the local authority instead of the Windows broker.</summary>
    /// <returns>Test completion.</returns>
    [Test]
    public async Task Startup_switch_registers_prompt_free_authority()
    {
        using var host = UIInspect.MCP.Server.Program.CreateHost(["--unrestricted"]);
        var options = host.Services.GetRequiredService<UiInspectOptions>();
        var authority = host.Services.GetRequiredService<IUnattendedApprovalAuthorizer>();
        var authorization = await authority.GetActiveLeaseAsync(UiCapability.Keyboard, CancellationToken.None);
        await Assert.That(options.Unrestricted).IsTrue();
        await Assert.That(authority is UnrestrictedStartupAuthorizer).IsTrue();
        await Assert.That(authorization!.ExpiresAtUtc).IsEqualTo(DateTimeOffset.MaxValue);
    }

    /// <summary>Startup authorization attaches directly and remains active without prompts, broker calls or rate limits.</summary>
    /// <returns>Test completion.</returns>
    [Test]
    public async Task Direct_attach_grants_all_capabilities_without_expiry_or_external_authorization()
    {
        await using var harness = new ServiceHarness(new UiInspectOptions { Unrestricted = true });
        harness.RateLimiter.Decisions.Enqueue(new(false, TimeSpan.FromMinutes(1)));
        var attached = await harness.Service.AttachAsync(harness.Target.ProcessId, null, ServiceHarness.ClientId, CancellationToken.None);
        await Assert.That(attached.Success).IsTrue();
        harness.Time.Advance(TimeSpan.FromDays(LongRunningDays));
        var inspected = await harness.Service.InspectAsync(attached.Data!.SessionId, 1, InspectionNodes, ServiceHarness.ClientId, CancellationToken.None);
        var invoked = await harness.Service.InvokeAsync(attached.Data.SessionId, ElementReference, ServiceHarness.ClientId, CancellationToken.None);
        var keyed = await harness.Service.SendKeyAsync(attached.Data.SessionId, ElementReference, "Enter", ServiceHarness.ClientId, CancellationToken.None);
        var discovered = await harness.Service.DiscoverAsync(ServiceHarness.ClientId, CancellationToken.None);
        var consent = await harness.Service.RequestConsentAsync(harness.Target.ProcessId, false, false, ServiceHarness.ClientId, CancellationToken.None);
        await Assert.That(inspected.Success).IsTrue();
        await Assert.That(invoked.Success).IsTrue();
        await Assert.That(keyed.Success).IsTrue();
        await Assert.That(discovered.Success).IsTrue();
        await Assert.That(consent.Data!.Origin).IsEqualTo(ConsentOrigin.UnrestrictedStartup);
        await Assert.That(consent.Data.ExpiresAtUtc).IsEqualTo(DateTimeOffset.MaxValue);
        await Assert.That(consent.Data.Capabilities).IsEqualTo(UiCapability.Inspect | UiCapability.Interact | UiCapability.Keyboard);
        await Assert.That(harness.Prompt.Requests.Count).IsEqualTo(0);
        await Assert.That(harness.UnattendedApprovals.Validations.Count).IsEqualTo(0);
        await Assert.That(harness.RateLimiter.Buckets.Count).IsEqualTo(0);
    }

    /// <summary>Unrestricted startup retains session ownership and PID reuse checks.</summary>
    /// <returns>Test completion.</returns>
    [Test]
    public async Task Unrestricted_sessions_still_reject_other_clients_and_reused_process_ids()
    {
        await using var harness = new ServiceHarness(new UiInspectOptions { Unrestricted = true });
        var attached = await harness.Service.AttachAsync(harness.Target.ProcessId, null, ServiceHarness.ClientId, CancellationToken.None);
        var otherClient = await harness.Service.InspectAsync(attached.Data!.SessionId, 1, InspectionNodes, "other-client", CancellationToken.None);
        await Assert.That(otherClient.Error!.Code).IsEqualTo("session_not_found");
        harness.Processes.Current = harness.Target with { StartedAtUtc = harness.Target.StartedAtUtc.AddSeconds(1) };
        var changed = await harness.Service.InspectAsync(attached.Data.SessionId, 1, InspectionNodes, ServiceHarness.ClientId, CancellationToken.None);
        await Assert.That(changed.Error!.Code).IsEqualTo("target_changed");
        await Assert.That(harness.Session.DisposeCalls).IsEqualTo(1);
    }

    /// <summary>Changing an options object after startup cannot elevate server authorization.</summary>
    /// <returns>Test completion.</returns>
    [Test]
    public async Task Runtime_option_mutation_cannot_enable_unrestricted_access()
    {
        await using var harness = new ServiceHarness();
        harness.Options.Unrestricted = true;
        var attached = await harness.Service.AttachAsync(harness.Target.ProcessId, null, ServiceHarness.ClientId, CancellationToken.None);
        await Assert.That(attached.Error!.Code).IsEqualTo("consent_required");
    }
}

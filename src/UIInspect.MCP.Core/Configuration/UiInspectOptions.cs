// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Core.Configuration;

/// <summary>MVP safety and response bounds.</summary>
public sealed class UiInspectOptions
{
    /// <summary>Default short-lived consent duration in minutes.</summary>
    private const double DefaultConsentDurationMinutes = 15;

    /// <summary>Default time allowed for a trusted Windows consent prompt.</summary>
    private const double DefaultConsentPromptTimeoutMinutes = 2;

    /// <summary>Gets or sets the short-lived consent duration.</summary>
    public TimeSpan ConsentDuration { get; set; } = TimeSpan.FromMinutes(DefaultConsentDurationMinutes);

    /// <summary>Gets or sets the fail-closed deadline for a trusted Windows consent prompt.</summary>
    public TimeSpan ConsentPromptTimeout { get; set; } = TimeSpan.FromMinutes(DefaultConsentPromptTimeoutMinutes);

    /// <summary>Gets or sets the maximum accepted inspection depth.</summary>
    public int MaximumTreeDepth { get; set; } = 12;

    /// <summary>Gets or sets the maximum accepted node count.</summary>
    public int MaximumTreeNodes { get; set; } = 1000;

    /// <summary>Gets or sets discovery calls per minute and client.</summary>
    public int DiscoveryRatePerMinute { get; set; } = 30;

    /// <summary>Gets or sets tree reads per minute and client/target.</summary>
    public int InspectionRatePerMinute { get; set; } = 60;

    /// <summary>Gets or sets actions per minute and client/target.</summary>
    public int ActionRatePerMinute { get; set; } = 10;

    /// <summary>Gets or sets genuinely new consent prompts per minute and client.</summary>
    public int ConsentPromptRatePerMinute { get; set; } = 3;
}

// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
namespace UIInspect.MCP.Core.Configuration;

/// <summary>MVP safety and response bounds.</summary>
public sealed class UiInspectOptions
{
    /// <summary>Gets or sets the short-lived consent duration.</summary>
    public TimeSpan ConsentDuration { get; set; } = TimeSpan.FromMinutes(15);

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

    /// <summary>Gets or sets consent prompts per minute and client/target.</summary>
    public int ConsentPromptRatePerMinute { get; set; } = 3;
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UIInspect.Sample.WinForms;

/// <summary>Creates the controls used by the deterministic fixture.</summary>
internal static class FixtureControls
{
    /// <summary>Creates the read-only semantic result element.</summary>
    /// <returns>The result text box.</returns>
    internal static TextBox CreateResultBox() => new() { Name = FixtureNames.ResultText, AccessibleName = "Last action result", Text = "ready", ReadOnly = true, Dock = DockStyle.Fill };

    /// <summary>Creates a semantically named command button.</summary>
    /// <param name="name">The stable UI Automation name.</param>
    /// <param name="accessibleName">The accessible description.</param>
    /// <param name="text">The displayed button text.</param>
    /// <returns>The configured button.</returns>
    internal static Button CreateButton(string name, string accessibleName, string text) => new()
    {
        Name = name, AccessibleName = accessibleName, Text = text, Width = FixtureDimensions.ControlWidth, AutoSize = true,
    };

    /// <summary>Creates the vertical actions panel.</summary>
    /// <returns>The configured actions panel.</returns>
    internal static FlowLayoutPanel CreateActionsPanel() => new()
    {
        Name = FixtureNames.ActionsPanel, AccessibleName = "Actions panel", Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
    };

    /// <summary>Creates the controls group.</summary>
    /// <param name="actions">The contained actions panel.</param>
    /// <returns>The configured controls group.</returns>
    internal static GroupBox CreateControlsGroup(FlowLayoutPanel actions)
    {
        var group = new GroupBox { Name = FixtureNames.ControlsGroup, AccessibleName = "Controls", Text = "Controls", Dock = DockStyle.Fill, Padding = new(FixtureDimensions.GroupPadding) };
        group.Controls.Add(actions);
        return group;
    }

    /// <summary>Creates a fixed-width editable text box.</summary>
    /// <param name="name">The stable UI Automation name.</param>
    /// <param name="accessibleName">The accessible description.</param>
    /// <param name="text">The initial text.</param>
    /// <returns>The configured text box.</returns>
    internal static TextBox CreateTextBox(string name, string accessibleName, string text) => new()
    {
        Name = name, AccessibleName = accessibleName, Text = text, Width = FixtureDimensions.ControlWidth,
    };
}

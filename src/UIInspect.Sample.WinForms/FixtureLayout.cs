// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Drawing;

namespace UIInspect.Sample.WinForms;

/// <summary>Composes the fixture's stable semantic control tree.</summary>
internal static class FixtureLayout
{
    /// <summary>Creates the complete content layout.</summary>
    /// <param name="result">The read-only result element.</param>
    /// <param name="setResult">Updates the result element.</param>
    /// <returns>The fixture content layout.</returns>
    internal static TableLayoutPanel Create(TextBox result, Action<string> setResult)
    {
        var actions = FixtureControls.CreateActionsPanel();
        AddSemanticControls(actions, setResult);
        var layout = CreateTable();
        layout.Controls.Add(CreateHeader(), FixtureDimensions.OnlyColumn, FixtureDimensions.HeaderRow);
        layout.Controls.Add(FixtureControls.CreateControlsGroup(actions), FixtureDimensions.OnlyColumn, FixtureDimensions.ControlsRow);
        layout.Controls.Add(result, FixtureDimensions.OnlyColumn, FixtureDimensions.FooterRow);
        return layout;
    }

    /// <summary>Adds the action controls in their stable order.</summary>
    /// <param name="actions">The actions panel.</param>
    /// <param name="setResult">Updates the result element.</param>
    private static void AddSemanticControls(FlowLayoutPanel actions, Action<string> setResult)
    {
        var invokeCount = 0;
        var invoke = FixtureControls.CreateButton(FixtureNames.InvokeButton, "Invoke action", "Invoke");
        invoke.Click += (_, _) => SetInvocationResult(setResult, ref invokeCount);
        var disabled = FixtureControls.CreateButton(FixtureNames.DisabledButton, "Disabled", "Disabled");
        disabled.Enabled = false;
        var value = FixtureControls.CreateTextBox(FixtureNames.ValueTextBox, "Editable value", "initial");
        value.TextChanged += (_, _) => setResult($"value:{value.Text}");
        actions.Controls.Add(invoke);
        actions.Controls.Add(disabled);
        actions.Controls.Add(value);
        actions.Controls.Add(CreatePassword());
        actions.Controls.Add(CreateColours(setResult));
        actions.Controls.Add(CreateEnabled(setResult));
        actions.Controls.Add(CreateNavigation(setResult));
        actions.Controls.AddRange(CreateKeyboard(setResult));
    }

    /// <summary>Increments and publishes the invocation count.</summary>
    /// <param name="setResult">Updates the result element.</param>
    /// <param name="invokeCount">The invocation count.</param>
    private static void SetInvocationResult(Action<string> setResult, ref int invokeCount)
    {
        invokeCount++;
        setResult($"invoked:{invokeCount}");
    }

    /// <summary>Creates the password element.</summary>
    /// <returns>The configured password text box.</returns>
    private static TextBox CreatePassword() => new()
    {
        Name = FixtureNames.PasswordBox, AccessibleName = "Sensitive password", Text = "not-returned", UseSystemPasswordChar = true, Width = FixtureDimensions.ControlWidth,
    };

    /// <summary>Creates the colour selector.</summary>
    /// <param name="setResult">Updates the result element.</param>
    /// <returns>The configured colour selector.</returns>
    private static ComboBox CreateColours(Action<string> setResult)
    {
        var colors = new ComboBox { Name = FixtureNames.ColorComboBox, AccessibleName = "Color selection", DropDownStyle = ComboBoxStyle.DropDownList, Width = FixtureDimensions.ControlWidth };
        colors.Items.AddRange(["Red", "Green", "Blue"]);
        colors.SelectedIndexChanged += (_, _) => setResult($"selection:{colors.SelectedItem}");
        colors.SelectedIndex = FixtureDimensions.HeaderRow;
        return colors;
    }

    /// <summary>Creates the enabled toggle.</summary>
    /// <param name="setResult">Updates the result element.</param>
    /// <returns>The configured enabled toggle.</returns>
    private static CheckBox CreateEnabled(Action<string> setResult)
    {
        var enabled = new CheckBox { Name = FixtureNames.EnabledCheckBox, AccessibleName = "Enabled toggle", Text = "Enabled", Checked = true, AutoSize = true };
        enabled.CheckedChanged += (_, _) => setResult($"toggle:{enabled.Checked.ToString().ToLowerInvariant()}");
        return enabled;
    }

    /// <summary>Creates the expandable navigation tree.</summary>
    /// <param name="setResult">Updates the result element.</param>
    /// <returns>The configured navigation tree.</returns>
    private static TreeView CreateNavigation(Action<string> setResult)
    {
        var tree = new TreeView { Name = FixtureNames.NavigationTree, AccessibleName = "Navigation tree", Width = FixtureDimensions.ControlWidth, Height = FixtureDimensions.TreeHeight };
        var root = new TreeNode("Root") { Name = "RootNode" };
        var child = new TreeNode("Child") { Name = "ChildNode" };
        var leaf = new TreeNode("Leaf") { Name = "LeafNode" };
        _ = child.Nodes.Add(leaf);
        _ = root.Nodes.Add(child);
        _ = tree.Nodes.Add(root);
        root.Expand();
        tree.AfterExpand += (_, args) => setResult($"expand:{args.Node?.Name ?? "unknown"}");
        tree.AfterCollapse += (_, args) => setResult($"collapse:{args.Node?.Name ?? "unknown"}");
        return tree;
    }

    /// <summary>Creates the keyboard target and its status element.</summary>
    /// <param name="setResult">Updates the result element.</param>
    /// <returns>The keyboard target and status element.</returns>
    private static Control[] CreateKeyboard(Action<string> setResult)
    {
        var keyboard = FixtureControls.CreateTextBox(FixtureNames.KeyboardTextBox, "Keyboard target", "focus me");
        var status = new Label { Name = FixtureNames.KeyStatusText, AccessibleName = "Keyboard status", Text = "key:none", AutoSize = true };
        keyboard.KeyDown += (_, args) => UpdateKeyStatus(status, args.KeyCode, setResult);
        return [keyboard, status];
    }

    /// <summary>Updates the keyboard status element.</summary>
    /// <param name="status">The keyboard status element.</param>
    /// <param name="keyCode">The received key.</param>
    /// <param name="setResult">Updates the result element.</param>
    private static void UpdateKeyStatus(Label status, Keys keyCode, Action<string> setResult)
    {
        status.Text = $"key:{keyCode}";
        setResult(status.Text);
    }

    /// <summary>Creates the outer table layout.</summary>
    /// <returns>The configured outer layout.</returns>
    private static TableLayoutPanel CreateTable()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new(FixtureDimensions.ContentPadding),
            RowCount = FixtureDimensions.LayoutRows,
            ColumnCount = FixtureDimensions.LayoutColumns,
        };
        _ = layout.RowStyles.Add(new(SizeType.Absolute, FixtureDimensions.HeaderFooterHeight));
        _ = layout.RowStyles.Add(new(SizeType.Percent, FixtureDimensions.ContentPercent));
        _ = layout.RowStyles.Add(new(SizeType.Absolute, FixtureDimensions.HeaderFooterHeight));
        return layout;
    }

    /// <summary>Creates the fixture header.</summary>
    /// <returns>The configured fixture header.</returns>
    private static Label CreateHeader()
    {
        var font = new Font(SystemFonts.DefaultFont.FontFamily, FixtureDimensions.HeaderFontSize, FontStyle.Bold);
        return new() { Text = "UIInspect semantic action fixture", Font = font, AutoSize = true };
    }
}

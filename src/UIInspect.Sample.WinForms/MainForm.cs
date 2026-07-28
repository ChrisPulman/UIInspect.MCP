// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Drawing;

namespace UIInspect.Sample.WinForms;

/// <summary>Deterministic WinForms UI Automation fixture.</summary>
public sealed class MainForm : Form
{
    private readonly Label _keyStatus;
    private readonly TextBox _result;
    private int _invokeCount;

    /// <summary>Initializes a new instance of the <see cref="MainForm"/> class.</summary>
    public MainForm()
    {
        Name = "SampleWinFormsForm";
        AccessibleName = "UIInspect WinForms Sample";
        Text = "UIInspect WinForms Sample";
        ClientSize = new Size(520, 620);
        StartPosition = FormStartPosition.CenterScreen;

        var actions = new FlowLayoutPanel
        {
            Name = "ActionsPanel",
            AccessibleName = "Actions panel",
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };

        var group = new GroupBox
        {
            Name = "ControlsGroup",
            AccessibleName = "Controls",
            Text = "Controls",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
        };
        group.Controls.Add(actions);

        var invoke = CreateButton("InvokeButton", "Invoke action", "Invoke");
        invoke.Click += (_, _) =>
        {
            _invokeCount++;
            SetResult($"invoked:{_invokeCount}");
        };

        var disabled = CreateButton("DisabledButton", "Disabled", "Disabled");
        disabled.Enabled = false;

        var value = new TextBox
        {
            Name = "ValueTextBox",
            AccessibleName = "Editable value",
            Text = "initial",
            Width = 430,
        };
        value.TextChanged += (_, _) => SetResult($"value:{value.Text}");

        var password = new TextBox
        {
            Name = "PasswordBox",
            AccessibleName = "Sensitive password",
            Text = "not-returned",
            UseSystemPasswordChar = true,
            Width = 430,
        };

        var colors = new ComboBox
        {
            Name = "ColorComboBox",
            AccessibleName = "Color selection",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 430,
        };
        colors.Items.AddRange(["Red", "Green", "Blue"]);
        colors.SelectedIndexChanged += (_, _) => SetResult($"selection:{colors.SelectedItem}");
        colors.SelectedIndex = 0;

        var enabled = new CheckBox
        {
            Name = "EnabledCheckBox",
            AccessibleName = "Enabled toggle",
            Text = "Enabled",
            Checked = true,
            AutoSize = true,
        };
        enabled.CheckedChanged += (_, _) => SetResult($"toggle:{enabled.Checked.ToString().ToLowerInvariant()}");

        var tree = new TreeView
        {
            Name = "NavigationTree",
            AccessibleName = "Navigation tree",
            Width = 430,
            Height = 150,
        };
        var rootNode = new TreeNode("Root") { Name = "RootNode" };
        var childNode = new TreeNode("Child") { Name = "ChildNode" };
        _ = childNode.Nodes.Add(new TreeNode("Leaf") { Name = "LeafNode" });
        _ = rootNode.Nodes.Add(childNode);
        _ = tree.Nodes.Add(rootNode);
        rootNode.Expand();
        tree.AfterExpand += (_, args) => SetResult($"expand:{args.Node?.Name ?? "unknown"}");
        tree.AfterCollapse += (_, args) => SetResult($"collapse:{args.Node?.Name ?? "unknown"}");

        var keyboard = new TextBox
        {
            Name = "KeyboardTextBox",
            AccessibleName = "Keyboard target",
            Text = "focus me",
            Width = 430,
        };

        _keyStatus = new Label
        {
            Name = "KeyStatusText",
            AccessibleName = "Keyboard status",
            Text = "key:none",
            AutoSize = true,
        };
        keyboard.KeyDown += (_, args) =>
        {
            _keyStatus.Text = $"key:{args.KeyCode}";
            SetResult(_keyStatus.Text);
        };

        _result = new TextBox
        {
            Name = "ResultText",
            AccessibleName = "Last action result",
            Text = "ready",
            ReadOnly = true,
            Dock = DockStyle.Fill,
        };

        actions.Controls.Add(invoke);
        actions.Controls.Add(disabled);
        actions.Controls.Add(value);
        actions.Controls.Add(password);
        actions.Controls.Add(colors);
        actions.Controls.Add(enabled);
        actions.Controls.Add(tree);
        actions.Controls.Add(keyboard);
        actions.Controls.Add(_keyStatus);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 3,
            ColumnCount = 1,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(
            new Label
            {
                Text = "UIInspect semantic action fixture",
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                AutoSize = true,
            },
            0,
            0);
        layout.Controls.Add(group, 0, 1);
        layout.Controls.Add(_result, 0, 2);
        Controls.Add(layout);
        SetResult("ready");
    }

    private static Button CreateButton(string name, string accessibleName, string text) =>
        new()
        {
            Name = name,
            AccessibleName = accessibleName,
            Text = text,
            Width = 430,
            AutoSize = true,
        };

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyStatus.Dispose();
            _result.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetResult(string text)
    {
        if (_result is not null)
        {
            _result.Text = text;
        }
    }
}

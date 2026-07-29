// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace UIInspect.Sample.WinUI;

/// <summary>Deterministic WinUI 3 UI Automation fixture window.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Tracks the number of times the invoke fixture has run.</summary>
    private int _invokeCount;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        ResultText.Text = "ready";
    }

    /// <summary>Records deterministic button invocation.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void InvokeButton_Click(object sender, RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(e);
        _invokeCount++;
        SetResult($"invoked:{_invokeCount}");
    }

    /// <summary>Removes the transient fixture target.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void RemoveTransientButton_Click(object sender, RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(e);
        _ = ActionsPanel.Children.Remove(TransientTarget);
        SetResult("transient:removed");
    }

    /// <summary>Reports the current text fixture value.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (sender is TextBox textBox)
        {
            SetResult($"value:{textBox.Text}");
        }
    }

    /// <summary>Reports the selected fixture colour.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (sender is ComboBox comboBox
            && comboBox.SelectedItem is ComboBoxItem item
            && item.Content is string color)
        {
            SetResult($"selection:{color}");
        }
    }

    /// <summary>Reports the enabled-state fixture value.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (sender is CheckBox checkBox)
        {
            SetResult($"toggle:{checkBox.IsChecked is true}");
        }
    }

    /// <summary>Reports expansion of the representative child node.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void ChildNode_Expanding(object sender, ExpanderExpandingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(e);
        SetResult("expand:Child");
    }

    /// <summary>Reports collapse of the representative child node.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void ChildNode_Collapsed(object sender, ExpanderCollapsedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(e);
        SetResult("collapse:Child");
    }

    /// <summary>Reports the latest keyboard fixture input.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void KeyboardTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);
        KeyStatusText.Text = $"key:{e.Key}";
        SetResult(KeyStatusText.Text);
    }

    /// <summary>Publishes an action result after the XAML control tree is available.</summary>
    /// <param name="result">The deterministic result value.</param>
    private void SetResult(string result)
    {
        if (ResultText is not null)
        {
            ResultText.Text = result;
        }
    }
}

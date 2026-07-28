// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UIInspect.Sample.Wpf;

/// <summary>Deterministic WPF UI Automation fixture window.</summary>
public partial class MainWindow : Window
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
        _invokeCount++;
        ResultText.Text = $"invoked:{_invokeCount}";
    }

    /// <summary>Removes the transient fixture target.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void RemoveTransientButton_Click(object sender, RoutedEventArgs e)
    {
        ((Panel)TransientTarget.Parent).Children.Remove(TransientTarget);
        ResultText.Text = "transient:removed";
    }

    /// <summary>Reports the current text fixture value.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ResultText is not null && sender is TextBox textBox)
        {
            ResultText.Text = $"value:{textBox.Text}";
        }
    }

    /// <summary>Reports the selected fixture colour.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultText is not null
            && sender is ComboBox { SelectedItem: ComboBoxItem item }
            && item.Content is string color)
        {
            ResultText.Text = $"selection:{color}";
        }
    }

    /// <summary>Reports the enabled-state fixture value.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (ResultText is not null && sender is CheckBox checkBox)
        {
            ResultText.Text = $"toggle:{checkBox.IsChecked is true}";
        }
    }

    /// <summary>Reports expansion of a tree fixture item.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void TreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (ResultText is not null && e.OriginalSource is TreeViewItem item)
        {
            ResultText.Text = $"expand:{item.Header}";
            e.Handled = true;
        }
    }

    /// <summary>Reports collapse of a tree fixture item.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void TreeItem_Collapsed(object sender, RoutedEventArgs e)
    {
        if (ResultText is not null && e.OriginalSource is TreeViewItem item)
        {
            ResultText.Text = $"collapse:{item.Header}";
            e.Handled = true;
        }
    }

    /// <summary>Reports the latest keyboard fixture input.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void KeyboardTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        KeyStatusText.Text = $"key:{e.Key}";
        ResultText.Text = KeyStatusText.Text;
    }
}

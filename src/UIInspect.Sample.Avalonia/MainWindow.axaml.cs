// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace UIInspect.Sample.Avalonia;

/// <summary>Hosts the deterministic Avalonia UI Automation fixture.</summary>
public partial class MainWindow : Window
{
    /// <summary>Indicates that XAML named controls are available for result updates.</summary>
    private bool _isInitialized;

    /// <summary>Tracks the number of times the invoke fixture has run.</summary>
    private int _invokeCount;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        _isInitialized = true;
        SetResult("ready");
    }

    /// <summary>Records deterministic button invocation.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    public void InvokeButton_Click(object? sender, RoutedEventArgs e)
    {
        _invokeCount++;
        SetResult($"invoked:{_invokeCount}");
    }

    /// <summary>Removes the transient fixture target.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    public void RemoveTransientButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = ActionsPanel.Children.Remove(TransientTarget);
        SetResult("transient:removed");
    }

    /// <summary>Reports the current text fixture value.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    public void ValueTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            SetResult($"value:{textBox.Text}");
        }
    }

    /// <summary>Reports the selected fixture colour.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    public void ColorComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
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
    public void EnabledCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            SetResult($"toggle:{checkBox.IsChecked is true}");
        }
    }

    /// <summary>Reports expansion of a tree fixture item.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    public void TreeItem_Expanded(object? sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem { Header: string header })
        {
            SetResult($"expand:{header}");
        }
    }

    /// <summary>Reports collapse of a tree fixture item.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    public void TreeItem_Collapsed(object? sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem { Header: string header })
        {
            SetResult($"collapse:{header}");
        }
    }

    /// <summary>Reports the latest keyboard fixture input.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    public void KeyboardTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        KeyStatusText.Text = $"key:{e.Key}";
        SetResult(KeyStatusText.Text);
    }

    /// <summary>Updates the read-only semantic result after XAML initialization.</summary>
    /// <param name="result">The deterministic result to display.</param>
    private void SetResult(string result)
    {
        if (_isInitialized)
        {
            ResultText.Text = result;
        }
    }
}

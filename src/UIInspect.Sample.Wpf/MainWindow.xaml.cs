// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UIInspect.Sample.Wpf;

/// <summary>Deterministic WPF UI Automation fixture window.</summary>
public partial class MainWindow : Window
{
    private int _invokeCount;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        ResultText.Text = "ready";
    }

    private void InvokeButton_Click(object sender, RoutedEventArgs e)
    {
        _invokeCount++;
        ResultText.Text = $"invoked:{_invokeCount}";
    }

    private void RemoveTransientButton_Click(object sender, RoutedEventArgs e)
    {
        ((Panel)TransientTarget.Parent).Children.Remove(TransientTarget);
        ResultText.Text = "transient:removed";
    }

    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ResultText is not null && sender is TextBox textBox)
        {
            ResultText.Text = $"value:{textBox.Text}";
        }
    }

    private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultText is not null &&
            sender is ComboBox { SelectedItem: ComboBoxItem item } &&
            item.Content is string color)
        {
            ResultText.Text = $"selection:{color}";
        }
    }

    private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (ResultText is not null && sender is CheckBox checkBox)
        {
            ResultText.Text = $"toggle:{checkBox.IsChecked is true}";
        }
    }

    private void TreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (ResultText is not null && e.OriginalSource is TreeViewItem item)
        {
            ResultText.Text = $"expand:{item.Header}";
            e.Handled = true;
        }
    }

    private void TreeItem_Collapsed(object sender, RoutedEventArgs e)
    {
        if (ResultText is not null && e.OriginalSource is TreeViewItem item)
        {
            ResultText.Text = $"collapse:{item.Header}";
            e.Handled = true;
        }
    }

    private void KeyboardTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        KeyStatusText.Text = $"key:{e.Key}";
        ResultText.Text = KeyStatusText.Text;
    }
}

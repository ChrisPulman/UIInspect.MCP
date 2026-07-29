// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Maui.Controls;

namespace UIInspect.Sample.Maui;

/// <summary>Deterministic MAUI UI Automation fixture page.</summary>
public partial class MainPage : ContentPage
{
    /// <summary>Tracks the number of times the invoke fixture has run.</summary>
    private int _invokeCount;

    /// <summary>Initializes a new instance of the <see cref="MainPage"/> class.</summary>
    public MainPage()
    {
        InitializeComponent();
        ColorComboBox.Items.Add("Red");
        ColorComboBox.Items.Add("Green");
        ColorComboBox.Items.Add("Blue");
        ColorComboBox.SelectedIndex = 0;
        ResultText.Text = "ready";
    }

    /// <summary>Records deterministic button invocation.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void InvokeButton_Clicked(object? sender, EventArgs e)
    {
        _invokeCount++;
        SetResult($"invoked:{_invokeCount}");
    }

    /// <summary>Removes the transient fixture target.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void RemoveTransientButton_Clicked(object? sender, EventArgs e)
    {
        _ = ActionsPanel.Children.Remove(TransientTarget);
        SetResult("transient:removed");
    }

    /// <summary>Reports the current text fixture value.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void ValueTextBox_TextChanged(object? sender, TextChangedEventArgs e) => SetResult($"value:{e.NewTextValue}");

    /// <summary>Reports the selected fixture colour.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void ColorComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ColorComboBox.SelectedItem is string color)
        {
            SetResult($"selection:{color}");
        }
    }

    /// <summary>Reports the enabled-state fixture value.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void EnabledCheckBox_CheckedChanged(object? sender, CheckedChangedEventArgs e) => SetResult($"toggle:{e.Value.ToString().ToLowerInvariant()}");

    /// <summary>Expands or collapses the navigation fixture's child nodes.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void RootNode_Clicked(object? sender, EventArgs e)
    {
        ChildNodes.IsVisible = !ChildNodes.IsVisible;
        SetResult($"{(ChildNodes.IsVisible ? "expand" : "collapse")}:Root");
    }

    /// <summary>Reports selection of the navigation child node.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void ChildNode_Clicked(object? sender, EventArgs e) => SetResult("select:Child");

    /// <summary>Reports completion of keyboard fixture input on platforms without a portable key event.</summary>
    /// <param name="sender">The control that raised the event.</param>
    /// <param name="e">The event data.</param>
    internal void KeyboardTextBox_Unfocused(object? sender, FocusEventArgs e)
    {
        KeyStatusText.Text = $"key:value:{KeyboardTextBox.Text}";
        SetResult(KeyStatusText.Text);
    }

    /// <summary>Updates the read-only result element.</summary>
    /// <param name="value">The deterministic fixture result.</param>
    private void SetResult(string value) => ResultText.Text = value;
}

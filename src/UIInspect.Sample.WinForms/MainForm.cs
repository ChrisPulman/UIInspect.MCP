// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace UIInspect.Sample.WinForms;

/// <summary>Hosts the deterministic WinForms UI Automation fixture.</summary>
public sealed class MainForm : Form
{
    /// <summary>Initializes a new instance of the <see cref="MainForm"/> class.</summary>
    public MainForm()
    {
        Name = FixtureNames.Form;
        AccessibleName = "UIInspect WinForms Sample";
        Text = AccessibleName;
        ClientSize = FixtureDimensions.FormClientSize;
        StartPosition = FormStartPosition.CenterScreen;

        var result = FixtureControls.CreateResultBox();
        Controls.Add(FixtureLayout.Create(result, text => result.Text = text));
        result.Text = "ready";
    }
}

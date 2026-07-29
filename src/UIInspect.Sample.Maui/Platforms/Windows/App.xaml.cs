// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace UIInspect.Sample.Maui.WinUI;

/// <summary>Windows activation host for the MAUI fixture application.</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>Creates the configured MAUI application host.</summary>
    /// <returns>The configured application.</returns>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

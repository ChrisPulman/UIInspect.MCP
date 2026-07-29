// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Maui.Hosting;

namespace UIInspect.Sample.Maui;

/// <summary>Configures the deterministic MAUI fixture application.</summary>
public static class MauiProgram
{
    /// <summary>Creates the MAUI application host.</summary>
    /// <returns>The configured MAUI application.</returns>
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder()
        .UseMauiApp<App>()
        .Build();
}

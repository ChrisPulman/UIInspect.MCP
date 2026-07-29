// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace UIInspect.Sample.Windows;

/// <summary>Locates the self-contained Windows App Runtime in the application directory.</summary>
internal static class WindowsAppRuntimeInitializer
{
    /// <summary>Configures the Windows App Runtime before application startup.</summary>
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
            AppContext.BaseDirectory);
}

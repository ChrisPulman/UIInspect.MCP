// Copyright (c) 2026 Chris Pulman.
// Licensed under the MIT license.
namespace UIInspect.Sample.WinForms;

/// <summary>WinForms fixture entry point.</summary>
internal static class Program
{
    /// <summary>Run the deterministic fixture window.</summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

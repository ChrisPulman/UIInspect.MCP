// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Server;

/// <summary>Result of installing or locating the bundled Codex skill.</summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Installed">Whether at least one skill file was copied.</param>
/// <param name="Skipped">Whether installation was safely skipped.</param>
/// <param name="Message">User-safe diagnostic message.</param>
/// <param name="TargetDirectory">Installed skill directory, when files were copied.</param>
internal sealed record SkillInstallResult(
    bool Success,
    bool Installed,
    bool Skipped,
    string Message,
    string? TargetDirectory)
{
    /// <summary>Create a successful installed result.</summary>
    /// <param name="message">Diagnostic message.</param>
    /// <param name="targetDirectory">Installed skill directory.</param>
    /// <returns>Installed result.</returns>
    internal static SkillInstallResult InstalledAt(string message, string targetDirectory) =>
        new(Success: true, Installed: true, Skipped: false, message, targetDirectory);

    /// <summary>Create a successful skipped result.</summary>
    /// <param name="message">Diagnostic message.</param>
    /// <returns>Skipped result.</returns>
    internal static SkillInstallResult SkippedWith(string message) =>
        new(Success: true, Installed: false, Skipped: true, message, TargetDirectory: null);

    /// <summary>Create a failed result.</summary>
    /// <param name="message">Diagnostic message.</param>
    /// <returns>Failed result.</returns>
    internal static SkillInstallResult Failure(string message) =>
        new(Success: false, Installed: false, Skipped: false, message, TargetDirectory: null);
}

// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
namespace UIInspect.MCP.Server;

/// <summary>Installs the bundled UIInspect skill into a Codex skills directory.</summary>
internal static class CodexSkillInstaller
{
    /// <summary>Bundled and installed skill directory name.</summary>
    internal const string SkillName = "uiinspect";

    /// <summary>Explicit skill-install command-line argument.</summary>
    private const string InstallArgument = "--install-codex-skill";

    /// <summary>Explicit overwrite command-line argument.</summary>
    private const string ForceArgument = "--force";

    /// <summary>Check whether explicit skill installation was requested.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>True when the install argument is present.</returns>
    internal static bool IsInstallRequested(IEnumerable<string> args) =>
        ContainsArgument(args, InstallArgument);

    /// <summary>Check whether existing skill files may be overwritten.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>True when the force argument is present.</returns>
    internal static bool IsForceRequested(IEnumerable<string> args) =>
        ContainsArgument(args, ForceArgument);

    /// <summary>Install the skill discovered beside or above the packaged executable.</summary>
    /// <param name="createCodexHome">Whether an absent Codex home may be created.</param>
    /// <param name="overwrite">Whether existing skill files may be overwritten.</param>
    /// <returns>The installation outcome.</returns>
    internal static SkillInstallResult InstallBundledSkill(bool createCodexHome, bool overwrite)
    {
        var sourceSkillDirectory = FindBundledSkillDirectory();
        if (sourceSkillDirectory is null)
        {
            return SkillInstallResult.Failure("The bundled UIInspect skill directory was not found.");
        }

        var codexHome = ResolveCodexHome(createCodexHome);
        return codexHome is null
            ? SkillInstallResult.SkippedWith(
                "Codex home was not found. Set CODEX_HOME or create %USERPROFILE%\\.codex.")
            : Install(sourceSkillDirectory, codexHome, overwrite);
    }

    /// <summary>Attempt a non-destructive install during ordinary MCP server startup.</summary>
    /// <param name="diagnostics">Standard-error diagnostics writer.</param>
    /// <returns>The installation outcome.</returns>
    internal static SkillInstallResult TryAutoInstall(TextWriter diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var result = InstallBundledSkill(createCodexHome: false, overwrite: false);
        if (result.Installed)
        {
            diagnostics.WriteLine(result.Message);
        }
        else if (!result.Success && !result.Skipped)
        {
            diagnostics.WriteLine($"Codex skill auto-install skipped: {result.Message}");
        }

        return result;
    }

    /// <summary>Copy one complete skill folder into a Codex home.</summary>
    /// <param name="sourceSkillDirectory">Source skill directory containing <c>SKILL.md</c>.</param>
    /// <param name="codexHome">Destination Codex home.</param>
    /// <param name="overwrite">Whether existing files may be overwritten.</param>
    /// <returns>The installation outcome.</returns>
    internal static SkillInstallResult Install(
        string sourceSkillDirectory,
        string codexHome,
        bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSkillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(codexHome);

        if (!Directory.Exists(sourceSkillDirectory))
        {
            return SkillInstallResult.Failure(
                $"Source skill directory does not exist: {sourceSkillDirectory}");
        }

        if (!File.Exists(Path.Combine(sourceSkillDirectory, "SKILL.md")))
        {
            return SkillInstallResult.Failure(
                $"Source skill directory is missing SKILL.md: {sourceSkillDirectory}");
        }

        var targetSkillDirectory = Path.Combine(codexHome, "skills", SkillName);
        if (AreSameDirectory(sourceSkillDirectory, targetSkillDirectory))
        {
            return SkillInstallResult.SkippedWith(
                $"Codex skill already points at {targetSkillDirectory}");
        }

        _ = Directory.CreateDirectory(targetSkillDirectory);

        var (copiedFiles, skippedFiles) = CopySkillFiles(
            sourceSkillDirectory,
            targetSkillDirectory,
            overwrite);
        if (copiedFiles == 0 && skippedFiles > 0)
        {
            return SkillInstallResult.SkippedWith(
                $"Codex skill already installed at {targetSkillDirectory}");
        }

        var verb = overwrite ? "Installed or updated" : "Installed";
        return SkillInstallResult.InstalledAt(
            $"{verb} Codex skill at {targetSkillDirectory}",
            targetSkillDirectory);
    }

    /// <summary>Copy source files into the installed skill directory.</summary>
    /// <param name="sourceSkillDirectory">Source skill directory.</param>
    /// <param name="targetSkillDirectory">Installed skill directory.</param>
    /// <param name="overwrite">Whether existing files may be overwritten.</param>
    /// <returns>Copied and skipped file counts.</returns>
    private static (int CopiedFiles, int SkippedFiles) CopySkillFiles(
        string sourceSkillDirectory,
        string targetSkillDirectory,
        bool overwrite)
    {
        var copiedFiles = 0;
        var skippedFiles = 0;
        foreach (var sourceFile in Directory.EnumerateFiles(
                     sourceSkillDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceSkillDirectory, sourceFile);
            var targetFile = Path.Combine(targetSkillDirectory, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                _ = Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetFile) && !overwrite)
            {
                skippedFiles++;
                continue;
            }

            File.Copy(sourceFile, targetFile, overwrite: true);
            copiedFiles++;
        }

        return (copiedFiles, skippedFiles);
    }

    /// <summary>Resolve the configured or default Codex home.</summary>
    /// <param name="create">Whether an absent directory may be created.</param>
    /// <returns>The full Codex home path, or null when unavailable.</returns>
    private static string? ResolveCodexHome(bool create)
    {
        var configuredHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var codexHome = !string.IsNullOrWhiteSpace(configuredHome)
            ? configuredHome
            : GetDefaultCodexHome();

        if (string.IsNullOrWhiteSpace(codexHome))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(codexHome));
        if (!Directory.Exists(fullPath))
        {
            if (!create)
            {
                return null;
            }

            _ = Directory.CreateDirectory(fullPath);
        }

        return fullPath;
    }

    /// <summary>Get the conventional per-user Codex home.</summary>
    /// <returns>The default path, or null when the user profile is unavailable.</returns>
    private static string? GetDefaultCodexHome()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? null
            : Path.Combine(userProfile, ".codex");
    }

    /// <summary>Find the bundled skill in development and packaged layouts.</summary>
    /// <returns>The skill directory, or null when it cannot be found.</returns>
    private static string? FindBundledSkillDirectory()
    {
        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "skills", SkillName);
            if (File.Exists(Path.Combine(candidate, "SKILL.md")))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Enumerate candidate development and NuGet tool roots.</summary>
    /// <returns>Candidate roots.</returns>
    private static IEnumerable<string> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;

        foreach (var root in SiblingAnyDirectories(AppContext.BaseDirectory))
        {
            yield return root;
        }

        foreach (var root in Ancestors(AppContext.BaseDirectory))
        {
            yield return root;
        }

        foreach (var root in Ancestors(Environment.CurrentDirectory))
        {
            yield return root;
        }
    }

    /// <summary>Enumerate sibling <c>any</c> folders used by .NET tool packages.</summary>
    /// <param name="startDirectory">Starting path.</param>
    /// <returns>Candidate sibling paths.</returns>
    private static IEnumerable<string> SiblingAnyDirectories(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            yield return Path.Combine(directory.FullName, "any");
            directory = directory.Parent;
        }
    }

    /// <summary>Enumerate ancestor directories.</summary>
    /// <param name="startDirectory">Starting path.</param>
    /// <returns>Ancestor paths.</returns>
    private static IEnumerable<string> Ancestors(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory.Parent is not null)
        {
            directory = directory.Parent;
            yield return directory.FullName;
        }
    }

    /// <summary>Compare two directory identities.</summary>
    /// <param name="left">First directory.</param>
    /// <param name="right">Second directory.</param>
    /// <returns>True when both paths identify the same directory.</returns>
    private static bool AreSameDirectory(string left, string right)
    {
        var normalizedLeft = Path.GetFullPath(left).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalizedRight = Path.GetFullPath(right).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Check for one case-insensitive command-line argument.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="expected">Expected argument.</param>
    /// <returns>True when present.</returns>
    private static bool ContainsArgument(IEnumerable<string> args, string expected)
    {
        foreach (var argument in args)
        {
            if (string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

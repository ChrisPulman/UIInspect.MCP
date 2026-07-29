// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using UIInspect.MCP.Server;

namespace UIInspect.MCP.Tests;

/// <summary>Tests safe installation of the bundled UIInspect Codex skill.</summary>
[NotInParallel]
public sealed class CodexSkillInstallerTests
{
    /// <summary>Conventional Codex home directory name.</summary>
    private const string CodexHomeDirectoryName = ".codex";

    /// <summary>Codex skills directory name.</summary>
    private const string SkillsDirectoryName = "skills";

    /// <summary>Required skill instruction file name.</summary>
    private const string SkillFileName = "SKILL.md";

    /// <summary>Skill UI metadata directory name.</summary>
    private const string AgentsDirectoryName = "agents";

    /// <summary>Skill UI metadata file name.</summary>
    private const string OpenAiMetadataFileName = "openai.yaml";

    /// <summary>Codex home environment variable.</summary>
    private const string CodexHomeEnvironmentVariable = "CODEX_HOME";

    /// <summary>Serializes process-wide environment changes.</summary>
    private static readonly Lock EnvironmentGate = new();

    /// <summary>Detects explicit install and overwrite arguments.</summary>
    /// <returns>A task that completes when assertions succeed.</returns>
    [Test]
    public async Task Arguments_detect_install_and_force_flags()
    {
        await Assert.That(
            CodexSkillInstaller.IsInstallRequested(["--install-codex-skill"])).IsTrue();
        await Assert.That(CodexSkillInstaller.IsInstallRequested(["--other"])).IsFalse();
        await Assert.That(
            CodexSkillInstaller.IsForceRequested(["--install-codex-skill", "--force"])).IsTrue();
    }

    /// <summary>Installs the packaged skill recursively into an explicitly configured Codex home.</summary>
    /// <returns>A task that completes when assertions succeed.</returns>
    [Test]
    public async Task Install_bundled_skill_uses_codex_home_and_copies_metadata()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var codexHome = Path.Combine(tempRoot, CodexHomeDirectoryName);
            SkillInstallResult result;
            lock (EnvironmentGate)
            {
                result = WithCodexHome(
                    codexHome,
                    static () => CodexSkillInstaller.InstallBundledSkill(
                        createCodexHome: true,
                        overwrite: false));
            }

            var installedSkill = Path.Combine(
                codexHome,
                SkillsDirectoryName,
                CodexSkillInstaller.SkillName);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Installed).IsTrue();
            await Assert.That(File.Exists(Path.Combine(installedSkill, SkillFileName))).IsTrue();
            await Assert.That(
                File.Exists(Path.Combine(
                    installedSkill,
                    AgentsDirectoryName,
                    OpenAiMetadataFileName))).IsTrue();
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>Does not create a Codex home during best-effort automatic installation.</summary>
    /// <returns>A task that completes when assertions succeed.</returns>
    [Test]
    public async Task Install_bundled_skill_skips_an_absent_codex_home()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var codexHome = Path.Combine(tempRoot, "missing-codex-home");
            SkillInstallResult result;
            lock (EnvironmentGate)
            {
                result = WithCodexHome(
                    codexHome,
                    static () => CodexSkillInstaller.InstallBundledSkill(
                        createCodexHome: false,
                        overwrite: false));
            }

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Skipped).IsTrue();
            await Assert.That(Directory.Exists(codexHome)).IsFalse();
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>Writes installation diagnostics only to the supplied writer.</summary>
    /// <returns>A task that completes when assertions succeed.</returns>
    [Test]
    public async Task Auto_install_reports_a_new_install()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var codexHome = Path.Combine(tempRoot, CodexHomeDirectoryName);
            _ = Directory.CreateDirectory(codexHome);
            await using var diagnostics = new StringWriter();
            SkillInstallResult result;
            lock (EnvironmentGate)
            {
                result = WithCodexHome(
                    codexHome,
                    () => CodexSkillInstaller.TryAutoInstall(diagnostics));
            }

            await Assert.That(result.Installed).IsTrue();
            await Assert.That(diagnostics.ToString()).Contains("Installed");
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>Copies all nested skill files and validates invalid sources.</summary>
    /// <returns>A task that completes when assertions succeed.</returns>
    [Test]
    public async Task Install_copies_nested_files_and_rejects_invalid_sources()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var sourceSkill = await CreateSourceSkillAsync(tempRoot);
            var codexHome = Path.Combine(tempRoot, CodexHomeDirectoryName);
            var installed = CodexSkillInstaller.Install(
                sourceSkill,
                codexHome,
                overwrite: false);
            var targetSkill = Path.Combine(
                codexHome,
                SkillsDirectoryName,
                CodexSkillInstaller.SkillName);

            var missing = CodexSkillInstaller.Install(
                Path.Combine(tempRoot, "missing"),
                codexHome,
                overwrite: false);
            var empty = Path.Combine(tempRoot, "empty");
            _ = Directory.CreateDirectory(empty);
            var invalid = CodexSkillInstaller.Install(empty, codexHome, overwrite: false);

            await Assert.That(installed.Success).IsTrue();
            await Assert.That(
                await File.ReadAllTextAsync(Path.Combine(targetSkill, SkillFileName))).Contains("uiinspect");
            await Assert.That(
                File.Exists(Path.Combine(
                    targetSkill,
                    AgentsDirectoryName,
                    OpenAiMetadataFileName))).IsTrue();
            await Assert.That(missing.Success).IsFalse();
            await Assert.That(invalid.Success).IsFalse();
            await Assert.That(invalid.Message).Contains(SkillFileName);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>Preserves user-modified files during automatic installation.</summary>
    /// <returns>A task that completes when assertions succeed.</returns>
    [Test]
    public async Task Install_does_not_overwrite_existing_files_without_force()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var sourceSkill = await CreateSourceSkillAsync(tempRoot);
            var codexHome = Path.Combine(tempRoot, CodexHomeDirectoryName);
            var targetSkill = Path.Combine(
                codexHome,
                SkillsDirectoryName,
                CodexSkillInstaller.SkillName);
            _ = Directory.CreateDirectory(targetSkill);
            var targetSkillFile = Path.Combine(targetSkill, SkillFileName);
            const string UserSkill = "user custom skill";
            await File.WriteAllTextAsync(targetSkillFile, UserSkill);

            var result = CodexSkillInstaller.Install(
                sourceSkill,
                codexHome,
                overwrite: false);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(targetSkillFile)).IsEqualTo(UserSkill);
            await Assert.That(
                File.Exists(Path.Combine(
                    targetSkill,
                    AgentsDirectoryName,
                    OpenAiMetadataFileName))).IsTrue();
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>Allows an explicit force operation to refresh installed files.</summary>
    /// <returns>A task that completes when assertions succeed.</returns>
    [Test]
    public async Task Install_overwrites_existing_files_when_forced()
    {
        var tempRoot = CreateTempRoot();
        try
        {
            var sourceSkill = await CreateSourceSkillAsync(tempRoot);
            var codexHome = Path.Combine(tempRoot, CodexHomeDirectoryName);
            var targetSkill = Path.Combine(
                codexHome,
                SkillsDirectoryName,
                CodexSkillInstaller.SkillName);
            _ = Directory.CreateDirectory(targetSkill);
            var targetSkillFile = Path.Combine(targetSkill, SkillFileName);
            await File.WriteAllTextAsync(targetSkillFile, "user custom skill");

            var result = CodexSkillInstaller.Install(
                sourceSkill,
                codexHome,
                overwrite: true);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Installed).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(targetSkillFile)).Contains("uiinspect");
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>Create a representative source skill tree.</summary>
    /// <param name="tempRoot">Temporary test root.</param>
    /// <returns>Source skill directory.</returns>
    private static async Task<string> CreateSourceSkillAsync(string tempRoot)
    {
        var sourceSkill = Path.Combine(
            tempRoot,
            "source",
            CodexSkillInstaller.SkillName);
        _ = Directory.CreateDirectory(Path.Combine(sourceSkill, AgentsDirectoryName));
        await File.WriteAllTextAsync(
            Path.Combine(sourceSkill, SkillFileName),
            "---\nname: uiinspect\ndescription: Test UIInspect skill.\n---\n");
        await File.WriteAllTextAsync(
            Path.Combine(sourceSkill, AgentsDirectoryName, OpenAiMetadataFileName),
            "interface:\n  display_name: \"UIInspect\"\n");
        return sourceSkill;
    }

    /// <summary>Create an isolated temporary directory.</summary>
    /// <returns>Created directory.</returns>
    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "uiinspect-mcp-tests",
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    /// <summary>Execute with a temporary Codex home environment setting.</summary>
    /// <param name="codexHome">Temporary Codex home.</param>
    /// <param name="action">Action to execute.</param>
    /// <returns>Action result.</returns>
    private static SkillInstallResult WithCodexHome(
        string codexHome,
        Func<SkillInstallResult> action)
    {
        var previousCodexHome = Environment.GetEnvironmentVariable(CodexHomeEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(CodexHomeEnvironmentVariable, codexHome);
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(CodexHomeEnvironmentVariable, previousCodexHome);
        }
    }

    /// <summary>Delete an isolated temporary directory.</summary>
    /// <param name="tempRoot">Temporary directory.</param>
    private static void DeleteTempRoot(string tempRoot) =>
        Directory.Delete(tempRoot, recursive: true);
}

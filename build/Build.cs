using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CP.BuildTools;
using Microsoft.Build.Construction;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace UIInspect.MCP.Build;

sealed partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    private static AbsolutePath SolutionFile => RootDirectory / "src" / "UIInspect.MCP.slnx";

    private static AbsolutePath NukeBuildProjectFile => RootDirectory / "build" / "_build.csproj";

    private static AbsolutePath ServerProjectFile => RootDirectory / "src" / "UIInspect.MCP.Server" / "UIInspect.MCP.Server.csproj";

    private static AbsolutePath McpManifestFile => RootDirectory / ".mcp" / "server.json";

    private static AbsolutePath ReadmeFile => RootDirectory / "README.md";

    readonly Solution Solution = SolutionFile.ReadSolution();
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    string _minVerVersion = string.Empty;
    string _packageVersion = string.Empty;

    static AbsolutePath PackagesDirectory => RootDirectory / "packages";

    IEnumerable<Project> ProductProjects => Solution.AllProjects.Where(
        project => !string.Equals(project.Path, NukeBuildProjectFile, StringComparison.OrdinalIgnoreCase));

    Target Print => _ => _
        .DependsOn(SynchronizeVersion)
        .Executes(() =>
        {
            Log.Information("Configuration = {Configuration}", Configuration);
            Log.Information("MinVerVersionOverride = {Value}", _minVerVersion);
            Log.Information("PackageVersion = {Value}", _packageVersion);
        });

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            if (IsLocalBuild)
            {
                return;
            }

            PackagesDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

    Target ResolveVersion => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var arguments =
                $"msbuild \"{ServerProjectFile}\" " +
                "-target:MinVer " +
                "-property:Restore=false " +
                "-getProperty:MinVerVersion,PackageVersion " +
                "-nologo -verbosity:quiet";
            var process = ProcessTasks.StartProcess(DotNetPath, arguments, RootDirectory);
            process.AssertWaitForExit();

            var output = string.Join(Environment.NewLine, process.Output.Select(line => line.Text));
            var jsonStart = output.IndexOf('{', StringComparison.Ordinal);
            if (jsonStart < 0)
            {
                throw new InvalidOperationException("MinVer did not return its calculated MSBuild properties.");
            }

            using var result = JsonDocument.Parse(output[jsonStart..]);
            var properties = result.RootElement.GetProperty("Properties");
            _minVerVersion = properties.GetProperty("MinVerVersion").GetString() ?? string.Empty;
            _packageVersion = properties.GetProperty("PackageVersion").GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_minVerVersion) || string.IsNullOrWhiteSpace(_packageVersion))
            {
                throw new InvalidOperationException("MinVer returned an empty version.");
            }

            Environment.SetEnvironmentVariable("MinVerVersionOverride", _minVerVersion);
        });

    Target SynchronizeVersion => _ => _
        .DependsOn(ResolveVersion)
        .Executes(() =>
        {
            SynchronizeMcpManifest();
            SynchronizeReadme();
            Log.Information("Synchronized MCP metadata to package version {PackageVersion}", _packageVersion);
        });

    Target Compile => _ => _
        .DependsOn(Print)
        .Executes(() =>
        {
            foreach (var project in ProductProjects)
            {
                DotNetBuild(s => s
                    .SetProjectFile(project)
                    .SetConfiguration(Configuration)
                    .SetNoRestore(true));
            }
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(ServerProjectFile)
                .SetConfiguration(Configuration)
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetOutputDirectory(PackagesDirectory));
            VerifyPackedPackage();
        });

    void SynchronizeMcpManifest()
    {
        var source = File.ReadAllText(McpManifestFile);
        var manifest = JsonNode.Parse(source)?.AsObject()
            ?? throw new InvalidOperationException("The MCP server manifest is not a JSON object.");
        manifest["version"] = _packageVersion;

        var packages = manifest["packages"]?.AsArray()
            ?? throw new InvalidOperationException("The MCP server manifest does not contain a packages array.");
        if (packages.Count == 0 || packages[0] is not JsonObject package)
        {
            throw new InvalidOperationException("The MCP server manifest does not contain a package object.");
        }

        package["version"] = _packageVersion;
        var updated = manifest
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .ReplaceLineEndings("\n")
            + "\n";
        WriteAllTextIfChanged(McpManifestFile, source, updated);
    }

    void SynchronizeReadme()
    {
        var source = File.ReadAllText(ReadmeFile);
        var packageCoordinateRegex = PackageCoordinateRegex();
        if (!packageCoordinateRegex.IsMatch(source))
        {
            throw new InvalidOperationException("The README does not contain a UIInspect.MCP.Server package coordinate.");
        }

        var updated = packageCoordinateRegex.Replace(
            source,
            _ => $"UIInspect.MCP.Server@{_packageVersion}");
        VerifyReadmePackageVersions(updated);
        WriteAllTextIfChanged(ReadmeFile, source, updated);
    }

    void VerifyPackedPackage()
    {
        var packageFile = PackagesDirectory / $"UIInspect.MCP.Server.{_packageVersion}.nupkg";
        if (!File.Exists(packageFile))
        {
            throw new InvalidOperationException($"The expected package was not created: {packageFile}");
        }

        using var archive = ZipFile.OpenRead(packageFile);
        var packagedReadme = ReadPackageEntry(archive, "README.md");
        var sourceReadme = File.ReadAllText(ReadmeFile);
        if (!string.Equals(packagedReadme, sourceReadme, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The packaged README does not match the synchronized repository README.");
        }

        VerifyReadmePackageVersions(packagedReadme);

        var manifest = JsonNode.Parse(ReadPackageEntry(archive, ".mcp/server.json"))?.AsObject()
            ?? throw new InvalidOperationException("The packaged MCP server manifest is not a JSON object.");
        VerifyExpectedVersion("packaged MCP manifest", manifest["version"]?.GetValue<string>());
        VerifyExpectedVersion(
            "packaged MCP package metadata",
            manifest["packages"]?[0]?["version"]?.GetValue<string>());

        var nuspecEntries = archive.Entries
            .Where(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (nuspecEntries.Count != 1)
        {
            throw new InvalidOperationException(
                $"The package must contain exactly one nuspec file; found {nuspecEntries.Count}.");
        }

        var nuspec = XDocument.Parse(ReadPackageEntry(nuspecEntries[0]));
        var nuspecVersion = nuspec
            .Descendants()
            .FirstOrDefault(static element => string.Equals(
                element.Name.LocalName,
                "version",
                StringComparison.Ordinal))
            ?.Value;
        VerifyExpectedVersion("NuGet package", nuspecVersion);

        Log.Information(
            "Verified package README, manifest, and nuspec use MinVer package version {PackageVersion}",
            _packageVersion);
    }

    void VerifyReadmePackageVersions(string readme)
    {
        var matches = PackageCoordinateRegex().Matches(readme);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException("The README does not contain a UIInspect.MCP.Server package coordinate.");
        }

        var mismatchedVersions = matches
            .Select(static match => match.Groups[1].Value)
            .Where(version => !string.Equals(version, _packageVersion, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (mismatchedVersions.Count != 0)
        {
            throw new InvalidOperationException(
                $"README package versions do not match {_packageVersion}: {string.Join(", ", mismatchedVersions)}");
        }
    }

    void VerifyExpectedVersion(string source, string actualVersion)
    {
        if (!string.Equals(actualVersion, _packageVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{source} version '{actualVersion}' does not match MinVer package version '{_packageVersion}'.");
        }
    }

    static string ReadPackageEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"The package does not contain {entryName}.");
        return ReadPackageEntry(entry);
    }

    static string ReadPackageEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(
        @"UIInspect\.MCP\.Server@([^\s"",`]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex PackageCoordinateRegex();

    static void WriteAllTextIfChanged(AbsolutePath path, string source, string updated)
    {
        if (string.Equals(source, updated, StringComparison.Ordinal))
        {
            return;
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, updated);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}

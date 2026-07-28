using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
        .Executes(() => DotNetPack(s => s
            .SetProject(ServerProjectFile)
            .SetConfiguration(Configuration)
            .SetNoBuild(true)
            .SetNoRestore(true)
            .SetOutputDirectory(PackagesDirectory)));

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
        var updated = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        WriteAllTextIfChanged(McpManifestFile, source, updated);
    }

    void SynchronizeReadme()
    {
        var source = File.ReadAllText(ReadmeFile);
        var commandRegex = DnxCommandRegex();
        if (!commandRegex.IsMatch(source))
        {
            throw new InvalidOperationException("The README does not contain the UIInspect.MCP.Server dnx command.");
        }

        var updated = commandRegex.Replace(
            source,
            _ => $"dnx UIInspect.MCP.Server@{_packageVersion} --yes");
        WriteAllTextIfChanged(ReadmeFile, source, updated);
    }

    [GeneratedRegex(@"dnx UIInspect\.MCP\.Server@\S+ --yes", RegexOptions.CultureInvariant)]
    private static partial Regex DnxCommandRegex();

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

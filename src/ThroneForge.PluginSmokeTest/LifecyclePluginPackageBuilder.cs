using System.Diagnostics;
using System.Security;
using ThroneForge.LoaderSmokeTest;

namespace ThroneForge.PluginSmokeTest;

public sealed record LifecyclePluginPackageBuildRequest(
    string RepositoryRoot,
    string CleanGameRoot,
    string PackageBuildRoot,
    string PackageRoot,
    string UnityAssemblyPath,
    string DotnetPath);

public interface ILifecyclePluginPackageBuilder
{
    void Build(LifecyclePluginPackageBuildRequest request);
}

/// <summary>
/// Builds the source-only lifecycle fixture into the owned experiment root.
/// The generated project and binaries are disposable experiment inputs and are never repository output.
/// </summary>
public sealed class SourceLifecyclePluginPackageBuilder : ILifecyclePluginPackageBuilder
{
    public void Build(LifecyclePluginPackageBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repositoryRoot = Path.GetFullPath(request.RepositoryRoot);
        var buildRoot = Path.GetFullPath(request.PackageBuildRoot);
        var packageRoot = Path.GetFullPath(request.PackageRoot);
        var templateRoot = Path.Combine(repositoryRoot, "templates", "lifecycle-plugin-smoke");
        var apiPath = Path.Combine(repositoryRoot, "artifacts", "bin", "ThroneForge.API", "Release", "netstandard2.1", "ThroneForge.API.dll");
        var contractsPath = Path.Combine(repositoryRoot, "artifacts", "bin", "ThroneForge.Contracts", "Release", "netstandard2.1", "ThroneForge.Contracts.dll");
        var bepinexPath = Path.Combine(request.CleanGameRoot, "BepInEx", "core", "BepInEx.dll");
        var unityDirectory = Path.GetDirectoryName(Path.GetFullPath(request.UnityAssemblyPath))
            ?? throw new PluginSmokeException("The Unity metadata directory is unavailable for package compilation.");
        var unityEnginePath = Path.Combine(unityDirectory, "UnityEngine.dll");
        var sourceRoot = Path.Combine(buildRoot, "source");
        var outputPackageRoot = packageRoot;

        RequireFile(Path.Combine(templateRoot, "PluginProject.csproj.template"));
        RequireFile(Path.Combine(templateRoot, "ThroneForgeLifecyclePlugin.cs"));
        RequireFile(Path.Combine(templateRoot, "LifecycleHost.cs"));
        RequireFile(apiPath);
        RequireFile(contractsPath);
        RequireFile(bepinexPath);
        RequireFile(unityEnginePath);
        RequireFile(request.UnityAssemblyPath);

        SmokeTestPathValidator.EnsureNoReparsePointsOnPath(buildRoot);
        SmokeTestPathValidator.EnsureNoReparsePointsOnPath(outputPackageRoot);
        if (Directory.Exists(outputPackageRoot)
            && Directory.EnumerateFileSystemEntries(outputPackageRoot).Any())
        {
            throw new PluginSmokeException("The owned lifecycle package directory must be empty before compilation.");
        }

        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(outputPackageRoot);
        File.Copy(Path.Combine(templateRoot, "ThroneForgeLifecyclePlugin.cs"), Path.Combine(sourceRoot, "ThroneForgeLifecyclePlugin.cs"));
        File.Copy(Path.Combine(templateRoot, "LifecycleHost.cs"), Path.Combine(sourceRoot, "LifecycleHost.cs"));

        var projectTemplate = File.ReadAllText(Path.Combine(templateRoot, "PluginProject.csproj.template"));
        var project = projectTemplate
            .Replace("__TARGET_FRAMEWORK__", LifecyclePluginPackageService.TargetFramework, StringComparison.Ordinal)
            .Replace("__BEPINEX_CORE__", EscapeXml(bepinexPath), StringComparison.Ordinal)
            .Replace("__UNITY_ENGINE__", EscapeXml(unityEnginePath), StringComparison.Ordinal)
            .Replace("__UNITY_CORE_MODULE__", EscapeXml(request.UnityAssemblyPath), StringComparison.Ordinal)
            .Replace("__THRONEFORGE_API__", EscapeXml(apiPath), StringComparison.Ordinal)
            .Replace("__THRONEFORGE_CONTRACTS__", EscapeXml(contractsPath), StringComparison.Ordinal);
        var projectPath = Path.Combine(sourceRoot, "ThroneForge.M1.LifecycleSmoke.csproj");
        File.WriteAllText(projectPath, project, new System.Text.UTF8Encoding(false));

        RunDotnetBuild(request.DotnetPath, projectPath);

        var builtPlugin = Path.Combine(sourceRoot, "bin", "Release", LifecyclePluginPackageService.TargetFramework, "ThroneForge.M1.LifecycleSmoke.dll");
        RequireFile(builtPlugin);
        File.Copy(builtPlugin, Path.Combine(outputPackageRoot, "ThroneForge.M1.LifecycleSmoke.dll"));
        File.Copy(apiPath, Path.Combine(outputPackageRoot, "ThroneForge.API.dll"));
        File.Copy(contractsPath, Path.Combine(outputPackageRoot, "ThroneForge.Contracts.dll"));
    }

    private static void RunDotnetBuild(string dotnetPath, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(dotnetPath))
        {
            throw new PluginSmokeException("An explicit .NET executable is required for the lifecycle package build.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetPath,
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--nologo");

        using var process = Process.Start(startInfo)
            ?? throw new PluginSmokeException("The lifecycle package compiler could not be started.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = standardOutput.GetAwaiter().GetResult();
        var error = standardError.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            _ = output;
            _ = error;
            throw new PluginSmokeException("The lifecycle package source did not compile successfully.");
        }
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new PluginSmokeException("A required lifecycle package input is missing.");
        }
    }

    private static string EscapeXml(string value)
        => SecurityElement.Escape(value) ?? throw new PluginSmokeException("A lifecycle package reference path could not be encoded safely.");
}

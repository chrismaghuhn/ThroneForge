using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Xunit;

namespace ThroneForge.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string[] SourceProjectNames =
    [
        "ThroneForge.Contracts",
        "ThroneForge.Schemas",
        "ThroneForge.API",
        "ThroneForge.Packaging",
        "ThroneForge.Diagnostics",
        "ThroneForge.Content",
        "ThroneForge.Logic",
        "ThroneForge.Runtime",
        "ThroneForge.GameAdapter.Abstractions",
        "ThroneForge.GameAdapter.Thronefall",
        "ThroneForge.Bootstrap.Thronefall",
        "ThroneForge.Cli",
        "ThroneForge.Studio",
        "ThroneForge.InGameUI",
        "ThroneForge.TestKit",
        "ThroneForge.Discovery",
        "ThroneForge.LoaderSmokeTest",
        "ThroneForge.PluginLoadTest"
    ];

    private static readonly Dictionary<string, string[]> AllowedProjectReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ThroneForge.Contracts"] = [],
            ["ThroneForge.Schemas"] = ["ThroneForge.Contracts"],
            ["ThroneForge.API"] = ["ThroneForge.Contracts"],
            ["ThroneForge.Packaging"] = ["ThroneForge.Contracts", "ThroneForge.Schemas"],
            ["ThroneForge.Diagnostics"] = ["ThroneForge.Contracts"],
            ["ThroneForge.Content"] = ["ThroneForge.Contracts", "ThroneForge.Schemas"],
            ["ThroneForge.Logic"] = ["ThroneForge.Contracts", "ThroneForge.Schemas"],
            ["ThroneForge.Runtime"] =
            [
                "ThroneForge.Contracts",
                "ThroneForge.Schemas",
                "ThroneForge.Packaging",
                "ThroneForge.Diagnostics",
                "ThroneForge.Content",
                "ThroneForge.Logic",
                "ThroneForge.GameAdapter.Abstractions"
            ],
            ["ThroneForge.GameAdapter.Abstractions"] = ["ThroneForge.Contracts"],
            ["ThroneForge.GameAdapter.Thronefall"] =
            [
                "ThroneForge.API",
                "ThroneForge.Content",
                "ThroneForge.Runtime",
                "ThroneForge.Diagnostics",
                "ThroneForge.GameAdapter.Abstractions"
            ],
            ["ThroneForge.Bootstrap.Thronefall"] =
            ["ThroneForge.Runtime", "ThroneForge.GameAdapter.Thronefall", "ThroneForge.Diagnostics"],
            ["ThroneForge.Cli"] =
            [
                "ThroneForge.Contracts",
                "ThroneForge.Schemas",
                "ThroneForge.Packaging",
                "ThroneForge.Content",
                "ThroneForge.Logic",
                "ThroneForge.Diagnostics"
            ],
            ["ThroneForge.Studio"] =
            [
                "ThroneForge.Contracts",
                "ThroneForge.Schemas",
                "ThroneForge.Packaging",
                "ThroneForge.Content",
                "ThroneForge.Logic",
                "ThroneForge.Diagnostics"
            ],
            ["ThroneForge.InGameUI"] = ["ThroneForge.Runtime", "ThroneForge.Diagnostics"],
            ["ThroneForge.TestKit"] =
            [
                "ThroneForge.Contracts",
                "ThroneForge.Schemas",
                "ThroneForge.Packaging",
                "ThroneForge.Diagnostics",
                "ThroneForge.Content",
                "ThroneForge.Logic",
                "ThroneForge.Runtime",
                "ThroneForge.GameAdapter.Abstractions"
            ],
            ["ThroneForge.Discovery"] = [],
            ["ThroneForge.LoaderSmokeTest"] = ["ThroneForge.Discovery"],
            ["ThroneForge.PluginLoadTest"] = ["ThroneForge.API", "ThroneForge.Contracts", "ThroneForge.Runtime"]
        };

    private static readonly string[] ForbiddenCoreReferenceTokens =
    [
        "UnityEngine",
        "BepInEx",
        "Harmony",
        "HarmonyX",
        "Assembly-CSharp",
        "GameAssembly"
    ];

    [Fact]
    public void M0ProjectSkeletonContainsAllDeclaredBoundaries()
    {
        var missing = SourceProjectNames
            .Where(name => !File.Exists(GetProjectPath(name)))
            .ToArray();

        Assert.True(missing.Length == 0, $"Missing M0 source projects: {string.Join(", ", missing)}");
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, "ThroneForge.slnx")), "Missing ThroneForge.slnx.");
    }

    [Fact]
    public void ProjectReferencesStayWithinTheDeclaredDependencyDirection()
    {
        var violations = new List<string>();

        foreach (var projectName in SourceProjectNames)
        {
            var projectPath = GetProjectPath(projectName);
            Assert.True(File.Exists(projectPath), $"Missing project file for {projectName}.");

            var document = XDocument.Load(projectPath);
            var references = document
                .Descendants("ProjectReference")
                .Select(element => Path.GetFileNameWithoutExtension((string?)element.Attribute("Include") ?? string.Empty))
                .Order(StringComparer.Ordinal)
                .ToArray();
            var allowed = AllowedProjectReferences[projectName].Order(StringComparer.Ordinal).ToArray();

            if (!references.SequenceEqual(allowed, StringComparer.Ordinal))
            {
                violations.Add($"{projectName}: expected [{string.Join(", ", allowed)}], found [{string.Join(", ", references)}]");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CoreProjectFilesDoNotReferenceGameRuntimeAssemblies()
    {
        var violations = new List<string>();

        foreach (var projectName in SourceProjectNames.Where(name =>
                     name is not "ThroneForge.GameAdapter.Thronefall" and not "ThroneForge.Bootstrap.Thronefall"))
        {
            var projectText = File.ReadAllText(GetProjectPath(projectName));
            foreach (var token in ForbiddenCoreReferenceTokens)
            {
                if (projectText.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{projectName} project file contains forbidden token '{token}'.");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void StudioAndCliDoNotReferenceTheConcreteGameAdapter()
    {
        foreach (var projectName in new[] { "ThroneForge.Cli", "ThroneForge.Studio" })
        {
            var projectText = File.ReadAllText(GetProjectPath(projectName));
            Assert.DoesNotContain("ThroneForge.GameAdapter.Thronefall", projectText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PluginApiAndRuntimeAdmissionBoundaryDoNotReferenceLoaderOrConcreteAdapter()
    {
        foreach (var projectName in new[] { "ThroneForge.API", "ThroneForge.Runtime" })
        {
            var projectText = File.ReadAllText(GetProjectPath(projectName));

            Assert.DoesNotContain("ThroneForge.GameAdapter.Thronefall", projectText, StringComparison.Ordinal);
            Assert.DoesNotContain("ThroneForge.LoaderSmokeTest", projectText, StringComparison.Ordinal);
            Assert.DoesNotContain("ThroneForge.Discovery", projectText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BuiltCoreAssembliesDoNotReferenceGameRuntimeAssemblies()
    {
        var missing = new List<string>();
        var violations = new List<string>();

        foreach (var projectName in SourceProjectNames.Where(name =>
                     name is not "ThroneForge.GameAdapter.Thronefall" and not "ThroneForge.Bootstrap.Thronefall"))
        {
            var assemblyPath = FindBuiltAssembly(projectName);
            if (assemblyPath is null)
            {
                missing.Add(projectName);
                continue;
            }

            // Keep this metadata-only so inspection never executes or loads the assembly.
            // The target framework remains provisional; revisit this if M1 introduces divergent TFMs.
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                continue;
            }

            var metadataReader = peReader.GetMetadataReader();
            foreach (var handle in metadataReader.AssemblyReferences)
            {
                var reference = metadataReader.GetAssemblyReference(handle);
                var referenceName = metadataReader.GetString(reference.Name);
                if (ForbiddenCoreReferenceTokens.Any(token =>
                        referenceName.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{projectName} references {referenceName}.");
                }
            }
        }

        Assert.True(missing.Count == 0, $"Missing built core assemblies: {string.Join(", ", missing)}");
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void RepositoryDependencyDeclarationsDoNotReferenceForbiddenRuntimeDependencies()
    {
        var findings = DependencyDeclarationScanner.Scan(
            DependencyDeclarationScanner.LoadRepositoryDeclarations(RepositoryRoot));

        Assert.True(
            findings.Count == 0,
            string.Join(
                Environment.NewLine,
                findings.Select(finding => $"{finding.RelativePath}: {finding.DependencyName}")));
    }

    private static string RepositoryRoot => FindRepositoryRoot();

    private static string GetProjectPath(string projectName) =>
        Path.Combine(RepositoryRoot, "src", projectName, $"{projectName}.csproj");

    private static string? FindBuiltAssembly(string projectName)
    {
        var artifacts = Path.Combine(RepositoryRoot, "artifacts", "bin", projectName);
        if (!Directory.Exists(artifacts))
        {
            return null;
        }

        return Directory
            .GetFiles(artifacts, $"{projectName}.dll", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the ThroneForge repository root.");
    }
}

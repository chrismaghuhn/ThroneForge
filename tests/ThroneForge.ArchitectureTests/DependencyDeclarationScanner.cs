using System.Text.Json;
using System.Xml.Linq;

namespace ThroneForge.ArchitectureTests;

internal sealed record DependencyDeclaration(string RelativePath, string Content, bool IsPermittedGameFacing);

internal sealed record DependencyFinding(string RelativePath, string DependencyName);

internal static class DependencyDeclarationScanner
{
    private static readonly string[] ForbiddenDependencyTokens =
    [
        "UnityEngine",
        "BepInEx",
        "Harmony",
        "HarmonyX",
        "HarmonyLib",
        "Assembly-CSharp",
        "GameAssembly"
    ];

    private static readonly string[] DependencyElementNames =
    [
        "PackageReference",
        "PackageVersion",
        "PackageDownload",
        "ProjectReference",
        "Reference",
        "FrameworkReference"
    ];

    public static IReadOnlyList<DependencyFinding> Scan(IEnumerable<DependencyDeclaration> declarations)
    {
        var findings = new List<DependencyFinding>();

        foreach (var declaration in declarations)
        {
            if (declaration.IsPermittedGameFacing)
            {
                continue;
            }

            foreach (var dependencyName in ExtractDependencyNames(declaration))
            {
                if (ForbiddenDependencyTokens.Any(token =>
                        dependencyName.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    findings.Add(new DependencyFinding(declaration.RelativePath, dependencyName));
                }
            }
        }

        return findings;
    }

    public static IReadOnlyList<DependencyDeclaration> LoadRepositoryDeclarations(string repositoryRoot)
    {
        var paths = new List<string>();
        foreach (var fileName in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            var path = Path.Combine(repositoryRoot, fileName);
            if (File.Exists(path))
            {
                paths.Add(path);
            }
        }

        var sourceRoot = Path.Combine(repositoryRoot, "src");
        if (Directory.Exists(sourceRoot))
        {
            paths.AddRange(Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories));
            paths.AddRange(Directory.EnumerateFiles(sourceRoot, "packages.lock.json", SearchOption.AllDirectories));
        }

        return paths
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
                return new DependencyDeclaration(
                    relativePath,
                    File.ReadAllText(path),
                    IsPermittedGameFacingPath(relativePath));
            })
            .ToArray();
    }

    private static string[] ExtractDependencyNames(DependencyDeclaration declaration)
    {
        if (declaration.RelativePath.EndsWith("packages.lock.json", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractLockFileDependencyNames(declaration.Content);
        }

        if (!declaration.RelativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            && !declaration.RelativePath.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
            && !declaration.RelativePath.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var document = XDocument.Parse(declaration.Content, LoadOptions.PreserveWhitespace);
        return document
            .Descendants()
            .Where(element => DependencyElementNames.Contains(element.Name.LocalName, StringComparer.Ordinal))
            .SelectMany(element => element.Attributes()
                .Where(attribute => attribute.Name.LocalName is "Include" or "Update" or "Remove")
                .Select(attribute => attribute.Value))
            .ToArray();
    }

    private static string[] ExtractLockFileDependencyNames(string content)
    {
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("dependencies", out var dependencies)
            || dependencies.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return dependencies
            .EnumerateObject()
            .Where(framework => framework.Value.ValueKind == JsonValueKind.Object)
            .SelectMany(framework => framework.Value.EnumerateObject())
            .Select(package => package.Name)
            .ToArray();
    }

    private static bool IsPermittedGameFacingPath(string relativePath) =>
        relativePath.StartsWith("src/ThroneForge.GameAdapter.Thronefall/", StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith("src/ThroneForge.Bootstrap.Thronefall/", StringComparison.OrdinalIgnoreCase);
}

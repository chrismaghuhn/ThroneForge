using Xunit;

namespace ThroneForge.ArchitectureTests;

public sealed class DependencyDeclarationScannerTests
{
    [Fact]
    public void FindsForbiddenPackageInProjectFile()
    {
        var findings = DependencyDeclarationScanner.Scan(
        [
            new DependencyDeclaration(
                "src/Fake/Fake.csproj",
                "<Project><ItemGroup><PackageReference Include=\"HarmonyLib\" /></ItemGroup></Project>",
                IsPermittedGameFacing: false)
        ]);

        Assert.Contains(findings, finding => finding.DependencyName == "HarmonyLib");
    }

    [Fact]
    public void FindsForbiddenPackageInCentralPackageFile()
    {
        var findings = DependencyDeclarationScanner.Scan(
        [
            new DependencyDeclaration(
                "Directory.Packages.props",
                "<Project><ItemGroup><PackageVersion Include=\"BepInEx.Core\" Version=\"1.0.0\" /></ItemGroup></Project>",
                IsPermittedGameFacing: false)
        ]);

        Assert.Contains(findings, finding => finding.DependencyName == "BepInEx.Core");
    }

    [Fact]
    public void AllowsTestPackageDeclarations()
    {
        var findings = DependencyDeclarationScanner.Scan(
        [
            new DependencyDeclaration(
                "tests/Fake.Tests/Fake.Tests.csproj",
                "<Project><ItemGroup><PackageReference Include=\"xunit\" /></ItemGroup></Project>",
                IsPermittedGameFacing: false)
        ]);

        Assert.Empty(findings);
    }

    [Fact]
    public void IgnoresHarmlessTextOutsideDependencyDeclarations()
    {
        var findings = DependencyDeclarationScanner.Scan(
        [
            new DependencyDeclaration(
                "README.md",
                "Harmony is a general design pattern; BepInEx is not configured here.",
                IsPermittedGameFacing: false)
        ]);

        Assert.Empty(findings);
    }

    [Fact]
    public void IgnoresForbiddenNamesInPermittedGameFacingProjectDeclarations()
    {
        var findings = DependencyDeclarationScanner.Scan(
        [
            new DependencyDeclaration(
                "src/ThroneForge.GameAdapter.Thronefall/ThroneForge.GameAdapter.Thronefall.csproj",
                "<Project><ItemGroup><PackageReference Include=\"BepInEx.Core\" /></ItemGroup></Project>",
                IsPermittedGameFacing: true)
        ]);

        Assert.Empty(findings);
    }
}


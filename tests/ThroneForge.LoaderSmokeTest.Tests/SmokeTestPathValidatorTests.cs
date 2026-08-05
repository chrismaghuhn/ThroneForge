using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class SmokeTestPathValidatorTests
{
    [Fact]
    public void RejectsExperimentRootInsideRepository()
    {
        using var fixture = new SmokeTestFixture();

        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateRoots(
            fixture.RepositoryRoot,
            fixture.GameRoot,
            Path.Combine(fixture.RepositoryRoot, "experiments")));
    }

    [Fact]
    public void RejectsExperimentRootInsideGame()
    {
        using var fixture = new SmokeTestFixture();

        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateRoots(
            fixture.RepositoryRoot,
            fixture.GameRoot,
            Path.Combine(fixture.GameRoot, "experiments")));
    }

    [Fact]
    public void AllowsExternalExperimentRootAndUsesSeparatorAwareComparison()
    {
        using var fixture = new SmokeTestFixture();

        var roots = SmokeTestPathValidator.ValidateRoots(
            fixture.RepositoryRoot,
            fixture.GameRoot,
            fixture.ExperimentRoot);

        Assert.Equal(Path.GetFullPath(fixture.ExperimentRoot), roots.ExperimentRoot);
        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.EnsureWithin(
            fixture.GameRoot,
            Path.Combine(fixture.GameRoot + "-reports", "result.md")));
    }

    [Fact]
    public void CleanupRejectsPathOutsideValidatedExperimentRoot()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(
            fixture.RepositoryRoot,
            fixture.GameRoot,
            fixture.ExperimentRoot);

        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateCleanupTarget(
            roots,
            fixture.GameRoot));
    }

    [Fact]
    public void RejectsExistingReparsePointExperimentRootWhenSupported()
    {
        using var fixture = new SmokeTestFixture();
        var target = Path.Combine(fixture.Root, "target");
        Directory.CreateDirectory(target);
        var link = Path.Combine(fixture.Root, "link");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateRoots(
            fixture.RepositoryRoot,
            fixture.GameRoot,
            link));
    }

    [Fact]
    public void PackageAndManifestPathsMustRemainOwnedByTheExperiment()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);

        var packageRoot = Path.Combine(roots.ExperimentRoot, "package");
        var manifest = Path.Combine(roots.ExperimentRoot, "manifests", "package.json");
        Assert.Equal(packageRoot, SmokeTestPathValidator.ValidateOwnedExperimentDirectory(roots, packageRoot, "package root"));
        Assert.Equal(manifest, SmokeTestPathValidator.ValidateOwnedExperimentFile(roots, manifest, "package manifest"));
        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateOwnedExperimentDirectory(roots, roots.CleanGameRoot, "package root"));
        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateOwnedExperimentFile(roots, fixture.GameRoot, "package manifest"));
    }

    [Fact]
    public void UnityMetadataPathMustMatchTheSelectedProfileDataDirectory()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        var unityPath = Path.Combine(fixture.GameRoot, "Thronefall_Data", "Managed", "UnityEngine.CoreModule.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(unityPath)!);
        File.WriteAllBytes(unityPath, [0]);

        Assert.Equal(unityPath, SmokeTestPathValidator.ValidateUnityAssemblyPath(roots, unityPath, "Thronefall.exe"));
        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateUnityAssemblyPath(roots, Path.Combine(fixture.Root, "UnityEngine.CoreModule.dll"), "Thronefall.exe"));
    }
}

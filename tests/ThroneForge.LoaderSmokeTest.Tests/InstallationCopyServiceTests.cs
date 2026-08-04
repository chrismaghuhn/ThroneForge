using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class InstallationCopyServiceTests
{
    [Fact]
    public void CopyManifestIsDeterministicAndComplete()
    {
        using var fixture = new SmokeTestFixture();
        Directory.CreateDirectory(Path.Combine(fixture.GameRoot, "nested"));
        File.WriteAllText(Path.Combine(fixture.GameRoot, "nested", "file.txt"), "nested");
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);

        var manifest = InstallationCopyService.Copy(roots);
        var captured = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);

        Assert.Equal(manifest.Files, captured.Files);
        Assert.Equal("game", File.ReadAllText(Path.Combine(roots.CleanGameRoot, "game.txt")));
        Assert.Equal("nested", File.ReadAllText(Path.Combine(roots.CleanGameRoot, "nested", "file.txt")));
    }

    [Fact]
    public void ReparsePointInsideOriginalStopsCopyWithoutRetainingCleanGame()
    {
        using var fixture = new SmokeTestFixture();
        var target = Path.Combine(fixture.Root, "outside");
        Directory.CreateDirectory(target);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(fixture.GameRoot, "escape"), target);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);

        Assert.Throws<SmokeTestException>(() => InstallationCopyService.Copy(roots));
        Assert.False(Directory.Exists(roots.CleanGameRoot));
    }
}

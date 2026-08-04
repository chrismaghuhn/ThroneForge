using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class LaunchObservationTests
{
    [Fact]
    public void ExecutableOutsideExperimentIsRejectedBeforeProcessStart()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        var outsideExecutable = Path.Combine(fixture.Root, "outside.exe");
        File.WriteAllText(outsideExecutable, "not an executable");

        Assert.Throws<SmokeTestException>(() => LaunchObservationService.ValidateExecutablePath(
            outsideExecutable,
            roots.CleanGameRoot,
            roots.ExperimentRoot));
    }
}

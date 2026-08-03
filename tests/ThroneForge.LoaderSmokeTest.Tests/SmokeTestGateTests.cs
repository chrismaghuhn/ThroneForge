using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class SmokeTestGateTests
{
    [Fact]
    public void BaselineFailurePreventsLoaderInstallation()
    {
        var baseline = new LaunchObservationResult(
            Started: false,
            StableInitialized: false,
            Exited: true,
            ExitCode: 1,
            ExecutableWasInsideExperiment: true,
            RequiresManualClosure: false,
            Elapsed: TimeSpan.Zero,
            FailureCategory: "process-exited-during-observation");

        Assert.Throws<SmokeTestException>(() => SmokeTestGates.RequireBaselineSuccess(baseline));
    }

    [Fact]
    public void OriginalPostCheckIsMandatory()
    {
        Assert.Throws<SmokeTestException>(() => SmokeTestGates.RequireOriginalUnchanged("a", "b"));
        SmokeTestGates.RequireOriginalUnchanged("A", "a");
    }

    [Fact]
    public void WrongFingerprintFailsBeforeExperimentDirectoryCreation()
    {
        using var fixture = new SmokeTestFixture();
        var expected = new string('a', 64);

        var exception = Assert.Throws<SmokeTestException>(() => SmokeTestOrchestrator.Run(new LoaderSmokeTestRequest(
            SmokeTestMode.Plan,
            fixture.GameRoot,
            fixture.ExperimentRoot,
            expected,
            fixture.RepositoryRoot,
            null,
            null)));

        Assert.Contains("fingerprint", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.ExperimentRoot));
        Assert.DoesNotContain(fixture.GameRoot, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

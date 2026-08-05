using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class LoaderLogParserTests
{
    [Fact]
    public void ParsesSuccessfulBepInExBootstrapWithoutRetainingRawLog()
    {
        const string log = """
            [Info   :   BepInEx] BepInEx 5.4.23.5
            [Info   :Preloader] Preloader finished
            [Info   :Chainloader] Chainloader initialized
            [Info   :Chainloader] 0 plugins to load
            [Info   :BepInEx] Configuration file generated
            """;

        var summary = LoaderLogParser.Parse(log);

        Assert.Equal("5.4.23.5", summary.BepInExVersion);
        Assert.True(summary.PreloaderInitialized);
        Assert.True(summary.ChainloaderInitialized);
        Assert.True(summary.ConfigurationGenerated);
        Assert.Equal(0, summary.PluginsDiscovered);
        Assert.True(summary.StableInitialized);
        Assert.DoesNotContain("Preloader finished", summary.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FatalLoaderErrorProducesFailedOutcome()
    {
        var summary = LoaderLogParser.Parse("[Error :Preloader] Fatal error while loading loader");

        Assert.Equal(SmokeTestOutcome.Failed, SmokeTestOutcomeClassifier.Classify(
            baselineSucceeded: true,
            loaderLaunchSucceeded: true,
            summary));
        Assert.True(summary.FatalErrorCount > 0);
    }

    [Fact]
    public void MissingLoaderEvidenceIsInconclusive()
    {
        var summary = LoaderLogParser.Parse("game started");

        Assert.Equal(SmokeTestOutcome.Inconclusive, SmokeTestOutcomeClassifier.Classify(
            baselineSucceeded: true,
            loaderLaunchSucceeded: true,
            summary));
    }

    [Fact]
    public void PreloaderAndChainloaderEvidenceIsEquivalentToGeneratedConfiguration()
    {
        var summary = LoaderLogParser.Parse("""
            BepInEx 5.4.23.5
            Preloader finished
            Chainloader initialized
            0 plugins to load
            """);

        Assert.True(summary.StableInitialized);
        Assert.False(summary.ConfigurationGenerated);
        Assert.Equal(SmokeTestOutcome.Passed, SmokeTestOutcomeClassifier.Classify(true, true, summary));
    }

    [Fact]
    public void CompleteBootstrapEvidenceAcceptsAProcessThatExitedDuringObservation()
    {
        var summary = LoaderLogParser.Parse("""
            BepInEx 5.4.23.5
            Preloader finished
            Chainloader initialized
            0 plugins to load
            """);
        var launch = new LaunchObservationResult(
            Started: true,
            StableInitialized: false,
            Exited: true,
            ExitCode: 0,
            ExecutableWasInsideExperiment: true,
            RequiresManualClosure: false,
            Elapsed: TimeSpan.FromMilliseconds(1),
            FailureCategory: "process-exited-during-observation");

        Assert.True(LoaderBootstrapLaunchCriteria.IsObserved(launch, summary));
        Assert.Equal(
            SmokeTestOutcome.Passed,
            SmokeTestOutcomeClassifier.Classify(true, LoaderBootstrapLaunchCriteria.IsObserved(launch, summary), summary));
    }

    [Fact]
    public void IncompleteBootstrapEvidenceDoesNotAcceptAnExitedProcess()
    {
        var summary = LoaderLogParser.Parse("BepInEx 5.4.23.5");
        var launch = new LaunchObservationResult(
            Started: true,
            StableInitialized: false,
            Exited: true,
            ExitCode: 0,
            ExecutableWasInsideExperiment: true,
            RequiresManualClosure: false,
            Elapsed: TimeSpan.FromMilliseconds(1),
            FailureCategory: "process-exited-during-observation");

        Assert.False(LoaderBootstrapLaunchCriteria.IsObserved(launch, summary));
        Assert.Equal(
            SmokeTestOutcome.Failed,
            SmokeTestOutcomeClassifier.Classify(true, LoaderBootstrapLaunchCriteria.IsObserved(launch, summary), summary));
    }
}

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

    [Fact]
    public void LoaderLaunchDiagnosticRetainsBoundedFactsWithoutRawLogOrPaths()
    {
        var launch = new LaunchObservationResult(
            Started: true,
            StableInitialized: false,
            Exited: true,
            ExitCode: 3,
            ExecutableWasInsideExperiment: true,
            RequiresManualClosure: false,
            Elapsed: TimeSpan.FromMilliseconds(25),
            FailureCategory: "process-exited-during-observation");
        var summary = LoaderLogParser.Parse("BepInEx 5.4.23.5\nPreloader started");

        var diagnostic = LoaderLaunchDiagnosticEvidence.Create(
            launch,
            new LoaderLogReadEvidence(true, true),
            summary,
            bootstrapObserved: false);

        Assert.Equal("process-exited-during-observation", diagnostic.LaunchCategory);
        Assert.Equal(LoaderLaunchDiagnosticCategories.PreloaderNotInitialized, diagnostic.DiagnosticCategory);
        Assert.True(diagnostic.ProcessStarted);
        Assert.True(diagnostic.ProcessExited);
        Assert.True(diagnostic.ExecutableInsideExperiment);
        Assert.Equal(3, diagnostic.ExitCode);
        Assert.Equal("5.4.23.5", diagnostic.BepInExVersion);
        Assert.False(diagnostic.PreloaderInitialized);
        Assert.False(diagnostic.ChainloaderInitialized);
        Assert.DoesNotContain("C:\\private\\game\\BepInEx\\LogOutput.log", diagnostic.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Preloader started", diagnostic.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingLoaderLogIsDistinguishedFromIncompleteBootstrap()
    {
        var launch = new LaunchObservationResult(
            Started: true,
            StableInitialized: false,
            Exited: true,
            ExitCode: 1,
            ExecutableWasInsideExperiment: true,
            RequiresManualClosure: false,
            Elapsed: TimeSpan.FromMilliseconds(1),
            FailureCategory: "process-exited-during-observation");

        var diagnostic = LoaderLaunchDiagnosticEvidence.Create(
            launch,
            new LoaderLogReadEvidence(false, false),
            null,
            bootstrapObserved: false);

        Assert.Equal("log-missing", diagnostic.DiagnosticCategory);
        Assert.False(diagnostic.LogPresent);
        Assert.False(diagnostic.LogReadable);
    }
}

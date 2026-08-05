using ThroneForge.PluginSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LifecycleOrchestrationTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";

    [Fact]
    public void TypedEvidenceDrivesACompleteResult()
    {
        var root = CreateRoot();
        try
        {
            var result = new LifecycleExperimentOrchestrator(
                root,
                Guid.NewGuid().ToString("N"),
                Fingerprint,
                new SuccessfulLifecycleOperations()).Run();

            Assert.Equal("Passed", result.OverallResult);
            Assert.Equal(LifecycleExperimentFailureCategories.StageCompleted, result.StableCategory);
            Assert.True(result.PluginRemovalVerified);
            Assert.True(result.LoaderRollbackVerified);
            Assert.True(result.DisposableRestorationVerified);
            Assert.True(result.OriginalManifestVerified);
            Assert.True(result.OriginalRuntimeVerified);
            Assert.True(result.OriginalLoaderIndicatorsAbsent);
            Assert.Equal("game/Thronefall.exe", result.SelectedExecutableRelativePath);
            Assert.Equal(new string('a', 64), result.PackageSha256);
            Assert.Equal(new string('b', 64), result.AdmissionBindingDigest);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingRequiredEvidenceCannotProducePassed()
    {
        var root = CreateRoot();
        try
        {
            var result = new LifecycleExperimentOrchestrator(
                root,
                Guid.NewGuid().ToString("N"),
                Fingerprint,
                new SuccessfulLifecycleOperations { OriginalPostcheckResult = new(true) }).Run();

            Assert.NotEqual("Passed", result.OverallResult);
            Assert.Equal(LifecycleExperimentFailureCategories.OriginalPostcheckFailed, result.StableCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SuppliedExecutableBindingMustMatchDiscoveredEvidence()
    {
        var root = CreateRoot();
        try
        {
            var result = new LifecycleExperimentOrchestrator(
                root,
                Guid.NewGuid().ToString("N"),
                Fingerprint,
                new SuccessfulLifecycleOperations(),
                expectedExecutableRelativePath: "other/Thronefall.exe").Run();

            Assert.Equal(LifecycleExperimentStage.OriginalPreflight, result.PrimaryFailedStage);
            Assert.Equal(LifecycleExperimentFailureCategories.OriginalPreflightFailed, result.PrimaryFailureCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PrimaryFailureSurvivesCleanupAndPostchecks()
    {
        var root = CreateRoot();
        try
        {
            var operations = new SuccessfulLifecycleOperations
            {
                LifecycleVerificationResult = new(false, LifecycleExperimentFailureCategories.LifecycleMarkerInvalid),
                PluginDeployed = true,
                LoaderApplied = true
            };

            var result = new LifecycleExperimentOrchestrator(
                root,
                operations.ExperimentId,
                Fingerprint,
                operations).Run();

            Assert.Equal(LifecycleExperimentStage.LifecycleVerification, result.PrimaryFailedStage);
            Assert.Equal(LifecycleExperimentFailureCategories.LifecycleMarkerInvalid, result.PrimaryFailureCategory);
            var state = LifecycleExperimentStageStateService.LoadAndValidate(root, operations.ExperimentId, Fingerprint);
            Assert.Equal(LifecycleExperimentStage.LifecycleVerification, state.PrimaryFailedStage);
            Assert.Equal(LifecycleExperimentFailureCategories.LifecycleMarkerInvalid, state.PrimaryFailureCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CleanupFailureIsSeparateFromPrimaryFailure()
    {
        var root = CreateRoot();
        try
        {
            var operations = new SuccessfulLifecycleOperations
            {
                LifecycleVerificationResult = new(false, LifecycleExperimentFailureCategories.LifecycleMarkerInvalid),
                PluginRemovalResult = new(false, LifecycleExperimentFailureCategories.PluginRemovalFailed),
                PluginDeployed = true,
                LoaderApplied = true
            };

            var result = new LifecycleExperimentOrchestrator(
                root,
                operations.ExperimentId,
                Fingerprint,
                operations).Run();

            Assert.Equal(LifecycleExperimentFailureCategories.LifecycleMarkerInvalid, result.PrimaryFailureCategory);
            Assert.Equal(LifecycleExperimentFailureCategories.PluginRemovalFailed, result.CleanupFailureCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoaderStateFailureCategoryIsRetained()
    {
        var root = CreateRoot();
        try
        {
            var operations = new SuccessfulLifecycleOperations
            {
                LoaderInstallResult = new(false, LifecycleExperimentFailureCategories.TransactionStateMissing)
            };

            var result = new LifecycleExperimentOrchestrator(
                root,
                operations.ExperimentId,
                Fingerprint,
                operations).Run();

            Assert.Equal(LifecycleExperimentStage.LoaderInstall, result.PrimaryFailedStage);
            Assert.Equal(LifecycleExperimentFailureCategories.TransactionStateMissing, result.PrimaryFailureCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AdmitAndDeployReportsVerificationFailureSeparately()
    {
        var root = CreateRoot();
        try
        {
            var operations = new SuccessfulLifecycleOperations
            {
                AdmitAndDeployResult = new(false, LifecycleExperimentFailureCategories.DeploymentVerificationFailed)
            };

            var result = new LifecycleExperimentOrchestrator(
                root,
                operations.ExperimentId,
                Fingerprint,
                operations).Run();

            Assert.Equal(LifecycleExperimentFailureCategories.DeploymentVerificationFailed, result.PrimaryFailureCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ActiveProcessEvidenceIsAppliedBeforeValidationAndSkipsCleanup()
    {
        var root = CreateRoot();
        try
        {
            var operations = new SuccessfulLifecycleOperations
            {
                LifecycleLaunchResult = new(false, LifecycleExperimentFailureCategories.ManualClosureRequired, ProcessActive: true)
            };

            var result = new LifecycleExperimentOrchestrator(
                root,
                operations.ExperimentId,
                Fingerprint,
                operations).Run();

            Assert.Equal("Inconclusive", result.OverallResult);
            Assert.True(result.RecoveryMarkerPersisted);
            Assert.False(operations.CleanupCalled);
            Assert.Equal(LifecycleExperimentFailureCategories.ManualClosureRequired, result.PrimaryFailureCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoaderLaunchFailureBeforeDeploymentSkipsPluginRemoval()
    {
        var root = CreateRoot();
        try
        {
            var operations = new SuccessfulLifecycleOperations
            {
                LoaderLaunchResult = new(false, LifecycleExperimentFailureCategories.LoaderLaunchFailed, LoaderApplied: true),
                LoaderApplied = true,
                PluginDeployed = false
            };

            var result = new LifecycleExperimentOrchestrator(
                root,
                operations.ExperimentId,
                Fingerprint,
                operations).Run();

            Assert.False(operations.PluginRemovalCalled);
            Assert.NotEqual(LifecycleExperimentFailureCategories.PluginRemovalFailed, result.CleanupFailureCategory);
            Assert.Equal(LifecycleExperimentFailureCategories.LoaderLaunchFailed, result.PrimaryFailureCategory);
            Assert.Equal(CleanupOperationStatus.NotRequired, result.PluginRemovalStatus);
            Assert.Equal(CleanupOperationStatus.Passed, result.LoaderRollbackStatus);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedReportDoesNotClaimQuittingWasObserved()
    {
        var root = CreateRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "discovery"));
        try
        {
            var result = new LifecycleExperimentResult(
                "Failed",
                LifecycleExperimentStage.OriginalPreflight,
                LifecycleExperimentStage.OriginalPreflight,
                null,
                LifecycleExperimentFailureCategories.OriginalPreflightFailed,
                true);

            var reportPath = new LifecycleExperimentReportWriter(root, Fingerprint).Write(result);
            var report = File.ReadAllText(reportPath);
            Assert.Contains("planned public UnityEngine.Application.quitting binding; event not verified", report, StringComparison.Ordinal);
            Assert.DoesNotContain("event observed while Thronefall was running", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RecoveryReportSeparatesCleanupFromTheFailedExperiment()
    {
        var root = CreateRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "discovery"));
        try
        {
            var writer = new LifecycleExperimentReportWriter(root, Fingerprint);
            writer.Write(new LifecycleExperimentResult(
                "Failed",
                LifecycleExperimentStage.LoaderLaunch,
                LifecycleExperimentStage.LoaderLaunch,
                LifecycleExperimentStage.LoaderInstall,
                LifecycleExperimentFailureCategories.LoaderLaunchFailed,
                true,
                PrimaryFailedStage: LifecycleExperimentStage.LoaderLaunch,
                PrimaryFailureCategory: LifecycleExperimentFailureCategories.LoaderLaunchFailed));

            var report = File.ReadAllText(writer.AppendRecovery(new LifecycleExperimentRollbackResult(
                "Passed",
                true,
                true,
                true,
                PluginRemovalStatus: CleanupOperationStatus.NotRequired,
                LoaderRollbackStatus: CleanupOperationStatus.Passed)));

            Assert.Contains("Overall result: Failed", report, StringComparison.Ordinal);
            Assert.Contains("Recovery result: Passed", report, StringComparison.Ordinal);
            Assert.Contains("Plugin removal: NotRequired", report, StringComparison.Ordinal);
            Assert.DoesNotContain("Application.quitting event observed while Thronefall was running", report, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-orchestrator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class SuccessfulLifecycleOperations : ILifecycleExperimentOperations
    {
        public string ExperimentId { get; } = Guid.NewGuid().ToString("N");
        public bool PluginDeployed { get; init; } = true;
        public bool LoaderApplied { get; init; } = true;
        public LifecycleStageEvidence? LoaderInstallResult { get; init; }
        public LifecycleStageEvidence? LoaderLaunchResult { get; init; }
        public LifecycleStageEvidence? AdmitAndDeployResult { get; init; }
        public LifecycleStageEvidence? LifecycleVerificationResult { get; init; }
        public LifecycleStageEvidence? PluginRemovalResult { get; init; }
        public LifecycleStageEvidence? OriginalPostcheckResult { get; init; }
        public LifecycleStageEvidence? LifecycleLaunchResult { get; init; }
        public bool CleanupCalled { get; private set; }
        public bool PluginRemovalCalled { get; private set; }

        public LifecycleStageEvidence EnsureOwnership(LifecycleExperimentContext context) => new(true);

        public OriginalPreflightEvidence OriginalPreflight(LifecycleExperimentContext context)
            => new(true, "game/Thronefall.exe", context.ExpectedFingerprint, true, true);

        public LifecycleStageEvidence DisposablePrepare(LifecycleExperimentContext context) => new(true);
        public LifecycleStageEvidence BaselineLaunch(LifecycleExperimentContext context) => new(true);

        public LifecycleStageEvidence LoaderInstall(LifecycleExperimentContext context)
            => LoaderInstallResult ?? new(true, LoaderTransactionStatus: "Applied", LoaderApplied: LoaderApplied);

        public LifecycleStageEvidence LoaderLaunch(LifecycleExperimentContext context)
            => LoaderLaunchResult ?? new(true, LoaderTransactionStatus: "LaunchObserved", LoaderApplied: true);

        public LoaderVerificationEvidence LoaderVerify(LifecycleExperimentContext context)
            => new(true, "LaunchObserved", true, true, true);

        public UnityMetadataEvidence UnityMetadataPreflight(LifecycleExperimentContext context)
            => new(true, "UnityEngine.CoreModule, Version=1.0.0.0");

        public PackageEvidence PackageBuild(LifecycleExperimentContext context)
            => new(true, new string('a', 64));

        public PackageEvidence PackageCapture(LifecycleExperimentContext context)
            => new(true, new string('a', 64));

        public DeploymentEvidence AdmitAndDeploy(LifecycleExperimentContext context)
            => new(AdmitAndDeployResult?.Succeeded ?? true,
                AdmitAndDeployResult?.FailureCategory,
                new string('a', 64),
                new string('b', 64),
                PluginDeployed);

        public LifecycleStageEvidence LifecycleLaunch(LifecycleExperimentContext context)
            => LifecycleLaunchResult ?? new(true);
        public LogStabilityEvidence LogStability(LifecycleExperimentContext context) => new(true, "stable log");

        public LifecycleVerificationEvidence LifecycleVerification(LifecycleExperimentContext context)
            => LifecycleVerificationResult is { } result
                ? new(result.Succeeded, result.FailureCategory)
                : new(true, null, 1, 1, 1, "1,2,3", "ThroneForge.API, Version=1.0.0.0", "ThroneForge.Contracts, Version=1.0.0.0", 1, 0, 0, 0);

        public CleanupEvidence PluginRemoval(LifecycleExperimentContext context)
        {
            PluginRemovalCalled = true;
            return new(PluginRemovalResult?.Succeeded ?? true, PluginRemovalResult?.FailureCategory, true, true);
        }

        public CleanupEvidence LoaderRollback(LifecycleExperimentContext context)
            => new(true, null, null, null, true);

        public PostcheckEvidence DisposablePostcheck(LifecycleExperimentContext context)
            => new(true, null, true, true, true, true);

        public PostcheckEvidence OriginalPostcheck(LifecycleExperimentContext context)
            => OriginalPostcheckResult is { } result
                ? new(result.Succeeded, result.FailureCategory)
                : new(true, null, true, true, true, true);

        public RecoveryEvidence PersistManualClosureRecovery(LifecycleExperimentContext context)
            => new(true, true, RollbackCommand: "rollback-lifecycle-experiment");

        public LifecycleStageEvidence FinalizeFailure(LifecycleExperimentContext context)
        {
            CleanupCalled = true;
            return new(true);
        }
    }
}

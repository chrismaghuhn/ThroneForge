using ThroneForge.Discovery;
using ThroneForge.LoaderSmokeTest;

namespace ThroneForge.PluginSmokeTest;

public sealed record LifecycleExperimentRecoveryOptions(
    string RepositoryRoot,
    string OriginalGameRoot,
    string ExperimentRoot,
    string ExpectedFingerprint,
    string BepInExArchivePath,
    string ExpectedBepInExDigest);

public sealed record LifecycleExperimentRollbackResult(
    string OverallResult,
    bool LoaderRollbackVerified,
    bool DisposableRestored,
    bool OriginalVerified,
    string? FailureCategory = null,
    CleanupOperationStatus PluginRemovalStatus = CleanupOperationStatus.NotRequired,
    CleanupOperationStatus LoaderRollbackStatus = CleanupOperationStatus.NotAttempted);

/// <summary>
/// Performs the explicit, post-manual-closure recovery path. It never mutates an active profile.
/// </summary>
public static class LifecycleExperimentRecoveryService
{
    public static LifecycleExperimentRollbackResult Rollback(LifecycleExperimentRecoveryOptions options)
    {
        try
        {
            var roots = SmokeTestPathValidator.ValidateRoots(
                options.RepositoryRoot,
                options.OriginalGameRoot,
                options.ExperimentRoot);
            var ownership = Task6ExperimentStateService.LoadAndValidate(
                roots.ExperimentRoot,
                options.ExpectedFingerprint);
            if (ownership.Status is not (Task6ExperimentStatus.Failed
                or Task6ExperimentStatus.ManualClosureRequired
                or Task6ExperimentStatus.PluginDeployed
                or Task6ExperimentStatus.LaunchObserved
                or Task6ExperimentStatus.LoaderApplied))
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryOwnershipInvalid);
            }

            if (PluginDeploymentService.IsProfileProcessActive(roots.CleanGameRoot))
            {
                return new("Inconclusive", false, false, false, LifecycleExperimentFailureCategories.RecoveryProcessActive, CleanupOperationStatus.NotAttempted, CleanupOperationStatus.NotAttempted);
            }

            var originalManifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
            DisposableProfileBaseline baseline;
            try
            {
                baseline = DisposableProfileBaselineService.LoadAndValidateSavedBaseline(
                    LoaderSmokeTestStatePaths.GetBaselinePath(roots),
                    options.ExpectedFingerprint,
                    originalManifest);
            }
            catch (Exception exception) when (exception is PluginSmokeException or SmokeTestException or IOException)
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryBaselineRestoreFailed);
            }

            var transactionPath = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
            if (!File.Exists(transactionPath))
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryTransactionMissing);
            }

            LoaderTransactionState transaction;
            try
            {
                transaction = LoaderTransactionStateService.LoadAndValidate(
                    transactionPath,
                    roots,
                    options.ExpectedFingerprint,
                    baseline.DisposableManifest,
                    [LoaderTransactionStatus.Applied, LoaderTransactionStatus.LaunchObserved, LoaderTransactionStatus.RollbackRequired]);
            }
            catch (Exception exception) when (exception is PluginSmokeException or SmokeTestException or IOException)
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryTransactionMismatch);
            }

            if (ownership.Status == Task6ExperimentStatus.Failed
                && transaction.Status is not (LoaderTransactionStatus.Applied
                    or LoaderTransactionStatus.LaunchObserved
                    or LoaderTransactionStatus.RollbackRequired))
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryOwnershipInvalid);
            }

            try
            {
                _ = LoaderTransactionStateService.CaptureRollbackGeneratedEvidence(
                    transaction.ExpectedAppliedManifest,
                    InstallationCopyService.CaptureManifest(roots.CleanGameRoot),
                    out _);
            }
            catch (Exception exception) when (exception is PluginSmokeException or SmokeTestException or IOException)
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryRuntimeDrift);
            }

            var pluginRemovalStatus = CleanupOperationStatus.NotRequired;
            if (ownership.PluginRelativeRoot is not null)
            {
                pluginRemovalStatus = CleanupOperationStatus.Passed;
                PluginDeploymentService.Remove(roots.CleanGameRoot, LifecyclePluginPackageService.PluginGuid);
                var pluginPath = Path.Combine(roots.CleanGameRoot, "BepInEx", "plugins", LifecyclePluginPackageService.PluginGuid);
                if (Directory.Exists(pluginPath))
                {
                    return Failed(LifecycleExperimentFailureCategories.RecoveryPluginRemovalFailed, CleanupOperationStatus.Failed);
                }

                if (ownership.LoaderOnlyManifest is null
                    || !LoaderOnlyProfileVerificationService.Compare(
                        ownership.LoaderOnlyManifest,
                        InstallationCopyService.CaptureManifest(roots.CleanGameRoot)).Matches)
                {
                    return Failed(LifecycleExperimentFailureCategories.RecoveryPluginRemovalFailed, CleanupOperationStatus.Failed);
                }
            }

            var rollback = SmokeTestOrchestrator.Run(new LoaderSmokeTestRequest(
                SmokeTestMode.Rollback,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                roots.RepositoryRoot,
                options.BepInExArchivePath,
                null,
                OfficialAssetDigest: options.ExpectedBepInExDigest));
            if (rollback.Outcome is SmokeTestOutcome.Failed or SmokeTestOutcome.Inconclusive)
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryLoaderRollbackFailed, CleanupOperationStatus.Passed, CleanupOperationStatus.Failed);
            }

            transaction = LoaderTransactionStateService.LoadAndValidate(
                LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                roots,
                options.ExpectedFingerprint,
                baseline.DisposableManifest,
                [LoaderTransactionStatus.RolledBack]);
            if (transaction.Status != LoaderTransactionStatus.RolledBack)
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryLoaderRollbackFailed, CleanupOperationStatus.Passed, CleanupOperationStatus.Failed);
            }

            var disposableRestored = VerifyProfile(roots, baseline.DisposableManifest, options.ExpectedFingerprint, "recovery-disposable");
            var originalVerified = VerifyOriginal(roots, baseline.OriginalManifest, options.ExpectedFingerprint, "recovery-original");
            if (!disposableRestored)
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryDisposableReadinessFailed, CleanupOperationStatus.Passed, CleanupOperationStatus.Passed, true, false);
            }

            var rolledBack = ownership with
            {
                Status = Task6ExperimentStatus.RolledBack,
                PluginRelativeRoot = null,
                LoaderTransactionStatus = LoaderTransactionStatus.RolledBack.ToString()
            };
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, rolledBack);
            if (!originalVerified)
            {
                return Failed(LifecycleExperimentFailureCategories.RecoveryOriginalPostcheckFailed, CleanupOperationStatus.Passed, CleanupOperationStatus.Passed, true, true);
            }

            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, rolledBack with { Status = Task6ExperimentStatus.Completed });
            Task6ExperimentStateService.ClearRecovery(roots.ExperimentRoot);
            return new("Passed", true, true, true, null, pluginRemovalStatus, CleanupOperationStatus.Passed);
        }
        catch (PluginSmokeStateException exception)
        {
            return Failed(exception.FailureCategory);
        }
        catch (Exception exception) when (exception is PluginSmokeException or SmokeTestException or DiscoveryException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failed(LifecycleExperimentFailureCategories.RecoveryLoaderRollbackFailed);
        }
    }

    private static LifecycleExperimentRollbackResult Failed(
        string category,
        CleanupOperationStatus pluginRemovalStatus = CleanupOperationStatus.NotRequired,
        CleanupOperationStatus loaderRollbackStatus = CleanupOperationStatus.NotAttempted,
        bool loaderRollbackVerified = false,
        bool disposableRestored = false)
        => new("Failed", loaderRollbackVerified, disposableRestored, false, category, pluginRemovalStatus, loaderRollbackStatus);

    private static bool VerifyProfile(SmokeTestRoots roots, CopyManifest expectedManifest, string expectedFingerprint, string evidenceName)
    {
        var manifestMatches = InstallationCopyService.CompareManifests(
            expectedManifest,
            InstallationCopyService.CaptureManifest(roots.CleanGameRoot)).Matches;
        var output = Path.Combine(roots.EvidenceRoot, evidenceName);
        Directory.CreateDirectory(output);
        var runtime = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            roots.CleanGameRoot,
            expectedFingerprint,
            output,
            true));
        var evidence = RuntimeCompatibilityEvidenceContract.Parse(
            RuntimeCompatibilityEvidenceContract.Serialize(runtime),
            expectedFingerprint);
        return manifestMatches && evidence.IsReadyForReversibleTest && evidence.LoaderIndicatorsAbsent;
    }

    private static bool VerifyOriginal(SmokeTestRoots roots, CopyManifest expectedManifest, string expectedFingerprint, string evidenceName)
    {
        var manifestMatches = InstallationCopyService.CompareManifests(
            expectedManifest,
            InstallationCopyService.CaptureManifest(roots.OriginalGameRoot)).Matches;
        var output = Path.Combine(roots.EvidenceRoot, evidenceName);
        Directory.CreateDirectory(output);
        var runtime = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            roots.OriginalGameRoot,
            expectedFingerprint,
            output,
            true));
        var evidence = RuntimeCompatibilityEvidenceContract.Parse(
            RuntimeCompatibilityEvidenceContract.Serialize(runtime),
            expectedFingerprint);
        return manifestMatches && evidence.IsReadyForReversibleTest && evidence.LoaderIndicatorsAbsent;
    }
}

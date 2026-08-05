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
    string? FailureCategory = null);

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
            if (ownership.Status is not (Task6ExperimentStatus.ManualClosureRequired
                or Task6ExperimentStatus.PluginDeployed
                or Task6ExperimentStatus.LaunchObserved
                or Task6ExperimentStatus.LoaderApplied))
            {
                return Failed(LifecycleExperimentFailureCategories.OwnershipStateInvalid);
            }

            if (PluginDeploymentService.IsProfileProcessActive(roots.CleanGameRoot))
            {
                return new("Inconclusive", false, false, false, LifecycleExperimentFailureCategories.ProcessActive);
            }

            var originalManifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
            var baseline = DisposableProfileBaselineService.LoadAndValidateSavedBaseline(
                LoaderSmokeTestStatePaths.GetBaselinePath(roots),
                options.ExpectedFingerprint,
                originalManifest);

            if (ownership.PluginRelativeRoot is not null)
            {
                PluginDeploymentService.Remove(roots.CleanGameRoot, LifecyclePluginPackageService.PluginGuid);
                var pluginPath = Path.Combine(roots.CleanGameRoot, "BepInEx", "plugins", LifecyclePluginPackageService.PluginGuid);
                if (Directory.Exists(pluginPath))
                {
                    return Failed(LifecycleExperimentFailureCategories.PluginRemovalFailed);
                }

                if (ownership.LoaderOnlyManifest is null
                    || !InstallationCopyService.CompareManifests(
                        ownership.LoaderOnlyManifest,
                        InstallationCopyService.CaptureManifest(roots.CleanGameRoot)).Matches)
                {
                    return Failed(LifecycleExperimentFailureCategories.PluginRemovalFailed);
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
                return Failed(LifecycleExperimentFailureCategories.LoaderRollbackFailed);
            }

            var transaction = LoaderTransactionStateService.LoadAndValidate(
                LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                roots,
                options.ExpectedFingerprint,
                baseline.DisposableManifest,
                [LoaderTransactionStatus.RolledBack]);
            if (transaction.Status != LoaderTransactionStatus.RolledBack)
            {
                return Failed(LifecycleExperimentFailureCategories.LoaderRollbackFailed);
            }

            var disposableRestored = VerifyProfile(roots, baseline.DisposableManifest, options.ExpectedFingerprint, "recovery-disposable");
            var originalVerified = VerifyOriginal(roots, baseline.OriginalManifest, options.ExpectedFingerprint, "recovery-original");
            if (!disposableRestored)
            {
                return Failed(LifecycleExperimentFailureCategories.DisposableRestorationFailed, true, false);
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
                return Failed(LifecycleExperimentFailureCategories.OriginalPostcheckFailed, true, true);
            }

            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, rolledBack with { Status = Task6ExperimentStatus.Completed });
            Task6ExperimentStateService.ClearRecovery(roots.ExperimentRoot);
            return new("Passed", true, true, true);
        }
        catch (PluginSmokeStateException exception)
        {
            return Failed(exception.FailureCategory);
        }
        catch (Exception exception) when (exception is PluginSmokeException or SmokeTestException or DiscoveryException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failed(LifecycleExperimentFailureCategories.LoaderRollbackFailed);
        }
    }

    private static LifecycleExperimentRollbackResult Failed(
        string category,
        bool loaderRollbackVerified = false,
        bool disposableRestored = false)
        => new("Failed", loaderRollbackVerified, disposableRestored, false, category);

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

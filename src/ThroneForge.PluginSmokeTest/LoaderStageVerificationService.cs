using ThroneForge.Contracts;
using ThroneForge.Discovery;
using ThroneForge.LoaderSmokeTest;

namespace ThroneForge.PluginSmokeTest;

public sealed record LoaderStageVerificationEvidence(
    string LoaderStatus,
    string BaselineManifestIdentity,
    bool TransactionBaselineMatched,
    bool AppliedProfileMatched,
    bool BootstrapEvidencePresent,
    bool BootstrapCriteria);

/// <summary>
/// Read-only verification of the Task-3 loader state. The CLI never parses its JSON.
/// </summary>
public static class LoaderStageVerificationService
{
    public static LoaderStageVerificationEvidence Verify(
        string repositoryRoot,
        string originalGameRoot,
        string experimentRoot,
        string expectedFingerprint,
        LoaderTransactionStatus expectedStatus)
    {
        var roots = SmokeTestPathValidator.ValidateRoots(repositoryRoot, originalGameRoot, experimentRoot);
        var ownership = LoadOwnership(experimentRoot, expectedFingerprint);
        if (ownership.Status is not (Task6ExperimentStatus.LoaderApplied
            or Task6ExperimentStatus.LaunchObserved
            or Task6ExperimentStatus.PluginDeployed))
        {
            throw new PluginSmokeStateException(PluginSmokeStateFailureCategories.OwnershipStateInvalid, "The owned experiment is not in a loader-ready state.");
        }

        CopyManifest originalManifest;
        try
        {
            originalManifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
        }
        catch (SmokeTestException exception)
        {
            throw new PluginSmokeStateException(PluginSmokeStateFailureCategories.BaselineStateMismatch, "The original installation manifest could not be verified.", exception);
        }

        var baselinePath = LoaderSmokeTestStatePaths.GetBaselinePath(roots);
        DisposableProfileBaseline baseline;
        try
        {
            baseline = DisposableProfileBaselineService.LoadAndValidateSavedBaseline(
                baselinePath,
                expectedFingerprint,
                originalManifest);
        }
        catch (Exception exception) when (exception is SmokeTestException or IOException or UnauthorizedAccessException)
        {
            throw new PluginSmokeStateException(
                File.Exists(baselinePath)
                    ? PluginSmokeStateFailureCategories.BaselineStateMismatch
                    : PluginSmokeStateFailureCategories.BaselineStateMissing,
                "The canonical disposable baseline could not be verified.",
                exception);
        }

        var transactionPath = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        if (!File.Exists(transactionPath))
        {
            throw new PluginSmokeStateException(PluginSmokeStateFailureCategories.TransactionStateMissing, "The canonical loader transaction state is missing.");
        }

        LoaderTransactionState transaction;
        try
        {
            transaction = LoaderTransactionStateService.LoadAndValidate(
                transactionPath,
                roots,
                expectedFingerprint,
                baseline.DisposableManifest,
                [expectedStatus]);
            LoaderTransactionStateService.VerifyAppliedProfile(roots, transaction);
        }
        catch (Exception exception) when (exception is SmokeTestException or IOException or UnauthorizedAccessException)
        {
            throw new PluginSmokeStateException(PluginSmokeStateFailureCategories.AppliedProfileDrift, "The persisted loader profile could not be verified.", exception);
        }

        var baselineIdentity = InstallationCopyService.ComputeManifestIdentity(baseline.DisposableManifest);
        var transactionBaselineMatched = transaction.BaselineManifestIdentity.Equals(baselineIdentity, StringComparison.OrdinalIgnoreCase);
        if (!transactionBaselineMatched)
        {
            throw new PluginSmokeStateException(PluginSmokeStateFailureCategories.TransactionStateMismatch, "The loader transaction is bound to a different disposable baseline.");
        }

        var bootstrapPresent = transaction.LaunchEvidence is not null;
        var bootstrapCriteria = transaction.LaunchEvidence?.MeetsBootstrapCriteria == true;
        if (expectedStatus == LoaderTransactionStatus.LaunchObserved && (!bootstrapPresent || !bootstrapCriteria))
        {
            throw new PluginSmokeStateException(PluginSmokeStateFailureCategories.BootstrapEvidenceInvalid, "The loader launch evidence does not meet the clean bootstrap criteria.");
        }

        return new LoaderStageVerificationEvidence(
            transaction.Status.ToString(),
            baselineIdentity,
            transactionBaselineMatched,
            true,
            bootstrapPresent,
            bootstrapCriteria);
    }

    private static Task6ExperimentState LoadOwnership(string experimentRoot, string expectedFingerprint)
    {
        try
        {
            return Task6ExperimentStateService.LoadAndValidate(experimentRoot, expectedFingerprint);
        }
        catch (Exception exception) when (exception is PluginSmokeException or IOException or UnauthorizedAccessException)
        {
            throw new PluginSmokeStateException(PluginSmokeStateFailureCategories.OwnershipStateInvalid, "The Task-6 ownership record could not be verified.", exception);
        }
    }
}

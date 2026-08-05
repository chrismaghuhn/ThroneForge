namespace ThroneForge.PluginSmokeTest;

public sealed record LifecycleExperimentContext(
    string ExperimentRoot,
    string ExperimentId,
    string ExpectedFingerprint,
    string RepositoryBaselineCommit);

public record LifecycleStageEvidence(
    bool Succeeded,
    string? FailureCategory = null,
    bool? LoaderApplied = null,
    bool? PluginDeployed = null,
    bool? ProcessActive = null,
    string? LoaderTransactionStatus = null);

public sealed record OriginalPreflightEvidence(
    bool Succeeded,
    string? SelectedExecutableRelativePath,
    string? Fingerprint,
    bool? RuntimeReady,
    bool? LoaderIndicatorsAbsent,
    string? FailureCategory = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory);

public sealed record LoaderVerificationEvidence(
    bool Succeeded,
    string? LoaderStatus,
    bool? TransactionBaselineMatched,
    bool? AppliedProfileMatched,
    bool? BootstrapEvidencePresent,
    string? FailureCategory = null,
    bool? LoaderApplied = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory, LoaderApplied: LoaderApplied, LoaderTransactionStatus: LoaderStatus);

public sealed record UnityMetadataEvidence(
    bool Succeeded,
    string? SourceAssemblyIdentity,
    string? FailureCategory = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory);

public sealed record PackageEvidence(
    bool Succeeded,
    string? PackageSha256,
    string? FailureCategory = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory);

public sealed record DeploymentEvidence(
    bool Succeeded,
    string? FailureCategory = null,
    string? PackageSha256 = null,
    string? AdmissionBindingDigest = null,
    bool? PluginDeployed = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory, PluginDeployed: PluginDeployed);

public sealed record LogStabilityEvidence(
    bool Succeeded,
    string? StableText,
    string? FailureCategory = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory);

public sealed record LifecycleVerificationEvidence(
    bool Succeeded,
    string? FailureCategory = null,
    int? InitializationCount = null,
    int? QuittingCount = null,
    int? ShutdownCount = null,
    string? MarkerEncounterOrder = null,
    string? RuntimeApiIdentity = null,
    string? RuntimeContractsIdentity = null,
    int? PluginCount = null,
    int? WarningCount = null,
    int? ErrorCount = null,
    int? FatalErrorCount = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory);

public sealed record CleanupEvidence(
    bool Succeeded,
    string? FailureCategory = null,
    bool? RemovalVerified = null,
    bool? LoaderOnlyManifestVerified = null,
    bool? RollbackVerified = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory);

public sealed record PostcheckEvidence(
    bool Succeeded,
    string? FailureCategory = null,
    bool? ManifestVerified = null,
    bool? RuntimeVerified = null,
    bool? LoaderIndicatorsAbsent = null,
    bool? RestorationVerified = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory);

public sealed record RecoveryEvidence(
    bool Succeeded,
    bool MarkerPersisted,
    string? FailureCategory = null,
    string? RollbackCommand = null)
    : LifecycleStageEvidence(Succeeded, FailureCategory, ProcessActive: true);

public interface ILifecycleExperimentOperations
{
    LifecycleStageEvidence EnsureOwnership(LifecycleExperimentContext context);
    OriginalPreflightEvidence OriginalPreflight(LifecycleExperimentContext context);
    LifecycleStageEvidence DisposablePrepare(LifecycleExperimentContext context);
    LifecycleStageEvidence BaselineLaunch(LifecycleExperimentContext context);
    LifecycleStageEvidence LoaderInstall(LifecycleExperimentContext context);
    LifecycleStageEvidence LoaderLaunch(LifecycleExperimentContext context);
    LoaderVerificationEvidence LoaderVerify(LifecycleExperimentContext context);
    UnityMetadataEvidence UnityMetadataPreflight(LifecycleExperimentContext context);
    PackageEvidence PackageBuild(LifecycleExperimentContext context);
    PackageEvidence PackageCapture(LifecycleExperimentContext context);
    DeploymentEvidence AdmitAndDeploy(LifecycleExperimentContext context);
    LifecycleStageEvidence LifecycleLaunch(LifecycleExperimentContext context);
    LogStabilityEvidence LogStability(LifecycleExperimentContext context);
    LifecycleVerificationEvidence LifecycleVerification(LifecycleExperimentContext context);
    CleanupEvidence PluginRemoval(LifecycleExperimentContext context);
    CleanupEvidence LoaderRollback(LifecycleExperimentContext context);
    PostcheckEvidence DisposablePostcheck(LifecycleExperimentContext context);
    PostcheckEvidence OriginalPostcheck(LifecycleExperimentContext context);
    RecoveryEvidence PersistManualClosureRecovery(LifecycleExperimentContext context);
    LifecycleStageEvidence FinalizeFailure(LifecycleExperimentContext context);
}

public sealed record LifecycleExperimentResult(
    string OverallResult,
    LifecycleExperimentStage CurrentStage,
    LifecycleExperimentStage? FailedStage,
    LifecycleExperimentStage? LastCompletedStage,
    string StableCategory,
    bool StageStatePersisted,
    LifecycleExperimentStage? PrimaryFailedStage = null,
    string? PrimaryFailureCategory = null,
    string? CleanupFailureCategory = null,
    string? SelectedExecutableRelativePath = null,
    string? LoaderTransactionStatus = null,
    string? UnitySourceAssemblyIdentity = null,
    string? PackageSha256 = null,
    string? AdmissionBindingDigest = null,
    int? InitializationCount = null,
    int? QuittingCount = null,
    int? ShutdownCount = null,
    string? MarkerEncounterOrder = null,
    string? RuntimeApiIdentity = null,
    string? RuntimeContractsIdentity = null,
    int? PluginCount = null,
    int? WarningCount = null,
    int? ErrorCount = null,
    int? FatalErrorCount = null,
    bool? PluginRemovalVerified = null,
    bool? LoaderRollbackVerified = null,
    bool? DisposableRestorationVerified = null,
    bool? OriginalManifestVerified = null,
    bool? OriginalRuntimeVerified = null,
    bool? OriginalLoaderIndicatorsAbsent = null,
    bool? RecoveryMarkerPersisted = null,
    string? RecoveryMarkerFailureCategory = null,
    string? RollbackCommand = null,
    bool LoaderApplied = false,
    bool PluginDeployed = false,
    bool ProcessActive = false);

/// <summary>
/// The single repository and private-experiment owner of Task 7 stage execution.
/// External I/O is behind typed operations; no report or stage interpretation lives in PowerShell.
/// </summary>
public sealed class LifecycleExperimentOrchestrator
{
    public static IReadOnlyList<LifecycleExperimentStage> RequiredStages { get; } =
    [
        LifecycleExperimentStage.OriginalPreflight,
        LifecycleExperimentStage.DisposablePrepare,
        LifecycleExperimentStage.BaselineLaunch,
        LifecycleExperimentStage.LoaderInstall,
        LifecycleExperimentStage.LoaderLaunch,
        LifecycleExperimentStage.LoaderVerify,
        LifecycleExperimentStage.UnityMetadataPreflight,
        LifecycleExperimentStage.PackageBuild,
        LifecycleExperimentStage.PackageCapture,
        LifecycleExperimentStage.AdmitAndDeploy,
        LifecycleExperimentStage.LifecycleLaunch,
        LifecycleExperimentStage.LogStability,
        LifecycleExperimentStage.LifecycleVerification,
        LifecycleExperimentStage.PluginRemoval,
        LifecycleExperimentStage.LoaderRollback,
        LifecycleExperimentStage.DisposablePostcheck,
        LifecycleExperimentStage.OriginalPostcheck
    ];

    private readonly string experimentRoot;
    private readonly string experimentId;
    private readonly string expectedFingerprint;
    private readonly string repositoryBaselineCommit;
    private readonly ILifecycleExperimentOperations operations;

    public LifecycleExperimentOrchestrator(
        string experimentRoot,
        string experimentId,
        string expectedFingerprint,
        ILifecycleExperimentOperations operations,
        string repositoryBaselineCommit = "test-baseline")
    {
        this.experimentRoot = experimentRoot;
        this.experimentId = experimentId;
        this.expectedFingerprint = expectedFingerprint;
        this.repositoryBaselineCommit = repositoryBaselineCommit;
        this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
    }

    public LifecycleExperimentResult Run()
    {
        var context = new LifecycleExperimentContext(experimentRoot, experimentId, expectedFingerprint, repositoryBaselineCommit);
        var accumulator = new LifecycleEvidenceAccumulator();
        var currentStage = RequiredStages[0];
        LifecycleExperimentStage? lastCompleted = null;
        LifecycleExperimentStage? primaryFailedStage = null;
        string? primaryFailureCategory = null;
        string? cleanupFailureCategory = null;
        var statePersisted = false;
        var ownershipEstablished = false;
        var loaderApplied = false;
        var pluginDeployed = false;
        var cleanupStarted = false;

        try
        {
            var ownershipEvidence = operations.EnsureOwnership(context);
            if (!ownershipEvidence.Succeeded || ownershipEvidence.FailureCategory is not null)
            {
                currentStage = LifecycleExperimentStage.DisposablePrepare;
                primaryFailedStage = LifecycleExperimentStage.DisposablePrepare;
                primaryFailureCategory = ownershipEvidence.FailureCategory ?? LifecycleExperimentFailureCategories.OwnershipStateInvalid;
            }
            else
            {
                ownershipEstablished = true;
                try
                {
                    LifecycleExperimentStageStateService.SaveAtomic(
                        experimentRoot,
                        LifecycleExperimentStageStateService.CreatePrepared(experimentId, expectedFingerprint));
                    statePersisted = true;
                }
                catch (Exception exception) when (exception is PluginSmokeException or IOException or UnauthorizedAccessException)
                {
                    primaryFailedStage = LifecycleExperimentStage.OriginalPreflight;
                    primaryFailureCategory = LifecycleExperimentFailureCategories.StageStatePersistenceFailed;
                }

                if (primaryFailedStage is null)
                {
                    foreach (var stage in RequiredStages)
                    {
                        currentStage = stage;
                        Persist(
                            currentStage,
                            lastCompleted,
                            LifecycleExperimentFailureCategories.InProgress,
                            ref statePersisted,
                            accumulator,
                            primaryFailedStage,
                            primaryFailureCategory,
                            cleanupFailureCategory);

                        var evidence = Execute(stage, context);
                        accumulator.Apply(stage, evidence);
                        loaderApplied |= evidence.LoaderApplied == true;
                        pluginDeployed |= evidence.PluginDeployed == true;
                        if (!IsValid(stage, evidence, context, out var category))
                        {
                            primaryFailedStage ??= currentStage;
                            primaryFailureCategory ??= category;
                            Persist(currentStage, lastCompleted, category, ref statePersisted, accumulator, primaryFailedStage, primaryFailureCategory, cleanupFailureCategory);
                            break;
                        }

                        lastCompleted = stage;
                        var index = RequiredStages.IndexOf(stage);
                        var next = index == RequiredStages.Count - 1 ? LifecycleExperimentStage.Completed : RequiredStages[index + 1];
                        Persist(next, lastCompleted, LifecycleExperimentFailureCategories.StageCompleted, ref statePersisted, accumulator, primaryFailedStage, primaryFailureCategory, cleanupFailureCategory);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            primaryFailedStage ??= currentStage;
            primaryFailureCategory ??= FailureCategoryFor(currentStage);
            Persist(currentStage, lastCompleted, primaryFailureCategory, ref statePersisted, accumulator, primaryFailedStage, primaryFailureCategory, cleanupFailureCategory);
        }
        finally
        {
            if (primaryFailedStage is not null && accumulator.ProcessActive)
            {
                try
                {
                    var recovery = operations.PersistManualClosureRecovery(context);
                    accumulator.Apply(LifecycleExperimentStage.LifecycleLaunch, recovery);
                    Persist(
                        LifecycleExperimentStage.LifecycleLaunch,
                        lastCompleted,
                        recovery.Succeeded && recovery.MarkerPersisted
                            ? LifecycleExperimentFailureCategories.ManualClosureRequired
                            : recovery.FailureCategory ?? LifecycleExperimentFailureCategories.ManualClosureRequired,
                        ref statePersisted,
                        accumulator,
                        primaryFailedStage,
                        primaryFailureCategory,
                        cleanupFailureCategory);
                }
                catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
                {
                    accumulator.Apply(
                        LifecycleExperimentStage.LifecycleLaunch,
                        new RecoveryEvidence(false, false, LifecycleExperimentFailureCategories.ManualClosureRequired));
                }
            }
            else if (primaryFailedStage is not null && (pluginDeployed || loaderApplied))
            {
                cleanupStarted = true;
                RunCleanup(context, currentStage, lastCompleted, ref statePersisted, accumulator, ref cleanupFailureCategory, primaryFailedStage, primaryFailureCategory);
            }

            if (primaryFailedStage is not null && ownershipEstablished && !accumulator.ProcessActive)
            {
                var finalization = operations.FinalizeFailure(context);
                if (!finalization.Succeeded || finalization.FailureCategory is not null)
                {
                    cleanupFailureCategory ??= finalization.FailureCategory ?? LifecycleExperimentFailureCategories.CleanupFailed;
                }
            }
        }

        var failed = primaryFailedStage is not null;
        var overall = failed
            ? primaryFailureCategory == LifecycleExperimentFailureCategories.ManualClosureRequired ? "Inconclusive" : "Failed"
            : "Passed";
        var stableCategory = primaryFailureCategory ?? LifecycleExperimentFailureCategories.StageCompleted;
        if (!failed && !accumulator.HasCompleteSuccessEvidence)
        {
            primaryFailedStage = LifecycleExperimentStage.OriginalPostcheck;
            primaryFailureCategory = LifecycleExperimentFailureCategories.OriginalPostcheckFailed;
            stableCategory = primaryFailureCategory;
            overall = "Failed";
        }

        if (cleanupStarted && cleanupFailureCategory is not null && primaryFailureCategory is null)
        {
            overall = "Failed";
            stableCategory = cleanupFailureCategory;
        }

        return accumulator.ToResult(
            overall,
            failed ? currentStage : LifecycleExperimentStage.Completed,
            primaryFailedStage,
            lastCompleted,
            stableCategory,
            statePersisted,
            primaryFailedStage,
            primaryFailureCategory,
            cleanupFailureCategory);
    }

    private LifecycleStageEvidence Execute(LifecycleExperimentStage stage, LifecycleExperimentContext context)
        => stage switch
        {
            LifecycleExperimentStage.OriginalPreflight => operations.OriginalPreflight(context),
            LifecycleExperimentStage.DisposablePrepare => operations.DisposablePrepare(context),
            LifecycleExperimentStage.BaselineLaunch => operations.BaselineLaunch(context),
            LifecycleExperimentStage.LoaderInstall => operations.LoaderInstall(context),
            LifecycleExperimentStage.LoaderLaunch => operations.LoaderLaunch(context),
            LifecycleExperimentStage.LoaderVerify => operations.LoaderVerify(context),
            LifecycleExperimentStage.UnityMetadataPreflight => operations.UnityMetadataPreflight(context),
            LifecycleExperimentStage.PackageBuild => operations.PackageBuild(context),
            LifecycleExperimentStage.PackageCapture => operations.PackageCapture(context),
            LifecycleExperimentStage.AdmitAndDeploy => operations.AdmitAndDeploy(context),
            LifecycleExperimentStage.LifecycleLaunch => operations.LifecycleLaunch(context),
            LifecycleExperimentStage.LogStability => operations.LogStability(context),
            LifecycleExperimentStage.LifecycleVerification => operations.LifecycleVerification(context),
            LifecycleExperimentStage.PluginRemoval => operations.PluginRemoval(context),
            LifecycleExperimentStage.LoaderRollback => operations.LoaderRollback(context),
            LifecycleExperimentStage.DisposablePostcheck => operations.DisposablePostcheck(context),
            LifecycleExperimentStage.OriginalPostcheck => operations.OriginalPostcheck(context),
            _ => throw new PluginSmokeException("The lifecycle experiment stage is unsupported.")
        };

    private static bool IsValid(LifecycleExperimentStage stage, LifecycleStageEvidence evidence, LifecycleExperimentContext context, out string category)
    {
        category = evidence.FailureCategory ?? FailureCategoryFor(stage);
        if (!evidence.Succeeded || evidence.FailureCategory is not null)
        {
            return false;
        }

        switch (stage)
        {
            case LifecycleExperimentStage.OriginalPreflight when evidence is OriginalPreflightEvidence preflight:
                return !string.IsNullOrWhiteSpace(preflight.SelectedExecutableRelativePath)
                    && string.Equals(preflight.Fingerprint, context.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase)
                    && preflight.RuntimeReady == true
                    && preflight.LoaderIndicatorsAbsent == true;
            case LifecycleExperimentStage.LoaderInstall:
                return string.Equals(evidence.LoaderTransactionStatus, "Applied", StringComparison.Ordinal);
            case LifecycleExperimentStage.LoaderLaunch:
                return string.Equals(evidence.LoaderTransactionStatus, "LaunchObserved", StringComparison.Ordinal);
            case LifecycleExperimentStage.LoaderVerify when evidence is LoaderVerificationEvidence loader:
                return loader.TransactionBaselineMatched == true && loader.AppliedProfileMatched == true && loader.BootstrapEvidencePresent == true;
            case LifecycleExperimentStage.UnityMetadataPreflight when evidence is UnityMetadataEvidence unity:
                return unity.SourceAssemblyIdentity?.StartsWith("UnityEngine.CoreModule", StringComparison.Ordinal) == true;
            case LifecycleExperimentStage.PackageBuild or LifecycleExperimentStage.PackageCapture when evidence is PackageEvidence package:
                return IsDigest(package.PackageSha256);
            case LifecycleExperimentStage.AdmitAndDeploy when evidence is DeploymentEvidence deployment:
                return IsDigest(deployment.PackageSha256) && IsDigest(deployment.AdmissionBindingDigest) && deployment.PluginDeployed == true;
            case LifecycleExperimentStage.LogStability when evidence is LogStabilityEvidence log:
                return !string.IsNullOrWhiteSpace(log.StableText);
            case LifecycleExperimentStage.LifecycleVerification when evidence is LifecycleVerificationEvidence lifecycle:
                return lifecycle.InitializationCount == 1
                    && lifecycle.QuittingCount == 1
                    && lifecycle.ShutdownCount == 1
                    && string.Equals(lifecycle.MarkerEncounterOrder, "1,2,3", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(lifecycle.RuntimeApiIdentity)
                    && !string.IsNullOrWhiteSpace(lifecycle.RuntimeContractsIdentity)
                    && lifecycle.PluginCount == 1
                    && lifecycle.ErrorCount == 0
                    && lifecycle.FatalErrorCount == 0;
            case LifecycleExperimentStage.PluginRemoval when evidence is CleanupEvidence removal:
                return removal.RemovalVerified == true && removal.LoaderOnlyManifestVerified == true;
            case LifecycleExperimentStage.LoaderRollback when evidence is CleanupEvidence rollback:
                return rollback.RollbackVerified == true;
            case LifecycleExperimentStage.DisposablePostcheck or LifecycleExperimentStage.OriginalPostcheck when evidence is PostcheckEvidence postcheck:
                return postcheck.ManifestVerified == true
                    && postcheck.RuntimeVerified == true
                    && postcheck.LoaderIndicatorsAbsent == true
                    && postcheck.RestorationVerified == true;
            default:
                return true;
        }
    }

    private void RunCleanup(
        LifecycleExperimentContext context,
        LifecycleExperimentStage currentStage,
        LifecycleExperimentStage? lastCompleted,
        ref bool statePersisted,
        LifecycleEvidenceAccumulator accumulator,
        ref string? cleanupFailureCategory,
        LifecycleExperimentStage? primaryFailedStage,
        string? primaryFailureCategory)
    {
        var cleanupLastCompleted = lastCompleted;
        var minimumCleanupIndex = Math.Max(
            RequiredStages.IndexOf(currentStage),
            lastCompleted is null ? -1 : RequiredStages.IndexOf(lastCompleted.Value));

        foreach (var stage in new[]
        {
            LifecycleExperimentStage.PluginRemoval,
            LifecycleExperimentStage.LoaderRollback,
            LifecycleExperimentStage.DisposablePostcheck,
            LifecycleExperimentStage.OriginalPostcheck
        })
        {
            if (RequiredStages.IndexOf(stage) <= minimumCleanupIndex)
            {
                continue;
            }

            var evidence = Execute(stage, context);
            if (!IsValid(stage, evidence, context, out var category))
            {
                accumulator.Apply(stage, evidence);
                cleanupFailureCategory ??= category;
                Persist(stage, cleanupLastCompleted, category, ref statePersisted, accumulator, primaryFailedStage, primaryFailureCategory, cleanupFailureCategory);
                continue;
            }

            accumulator.Apply(stage, evidence);
            cleanupLastCompleted = stage;
            var cleanupIndex = RequiredStages.IndexOf(stage);
            var next = cleanupIndex == RequiredStages.Count - 1
                ? LifecycleExperimentStage.Completed
                : RequiredStages[cleanupIndex + 1];
            Persist(next, cleanupLastCompleted, LifecycleExperimentFailureCategories.StageCompleted, ref statePersisted, accumulator, primaryFailedStage, primaryFailureCategory, cleanupFailureCategory);
        }
    }

    private void Persist(
        LifecycleExperimentStage current,
        LifecycleExperimentStage? lastCompleted,
        string category,
        ref bool statePersisted,
        LifecycleEvidenceAccumulator accumulator,
        LifecycleExperimentStage? primaryFailedStage,
        string? primaryFailureCategory,
        string? cleanupFailureCategory)
    {
        try
        {
            LifecycleExperimentStageStateService.Advance(
                experimentRoot,
                experimentId,
                expectedFingerprint,
                current,
                lastCompleted,
                category,
                accumulator.LoaderTransactionStatus,
                accumulator.PackageSha256,
                accumulator.AdmissionBindingDigest,
                primaryFailedStage,
                primaryFailureCategory,
                cleanupFailureCategory);
            statePersisted = true;
        }
        catch (Exception exception) when (exception is PluginSmokeException or IOException or UnauthorizedAccessException)
        {
            statePersisted = false;
        }
    }

    private static bool IsDigest(string? value)
        => value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string FailureCategoryFor(LifecycleExperimentStage stage)
        => stage switch
        {
            LifecycleExperimentStage.OriginalPreflight => LifecycleExperimentFailureCategories.OriginalPreflightFailed,
            LifecycleExperimentStage.DisposablePrepare => LifecycleExperimentFailureCategories.DisposablePrepareFailed,
            LifecycleExperimentStage.BaselineLaunch => LifecycleExperimentFailureCategories.BaselineLaunchFailed,
            LifecycleExperimentStage.LoaderInstall => LifecycleExperimentFailureCategories.LoaderInstallFailed,
            LifecycleExperimentStage.LoaderLaunch => LifecycleExperimentFailureCategories.LoaderLaunchFailed,
            LifecycleExperimentStage.LoaderVerify => LifecycleExperimentFailureCategories.LoaderVerifyFailed,
            LifecycleExperimentStage.UnityMetadataPreflight => LifecycleExperimentFailureCategories.UnityMetadataPreflightFailed,
            LifecycleExperimentStage.PackageBuild => LifecycleExperimentFailureCategories.PackageBuildFailed,
            LifecycleExperimentStage.PackageCapture => LifecycleExperimentFailureCategories.PackageCaptureFailed,
            LifecycleExperimentStage.AdmitAndDeploy => LifecycleExperimentFailureCategories.DeploymentFailed,
            LifecycleExperimentStage.LifecycleLaunch => LifecycleExperimentFailureCategories.LifecycleLaunchFailed,
            LifecycleExperimentStage.LogStability => LifecycleExperimentFailureCategories.LogNotStable,
            LifecycleExperimentStage.LifecycleVerification => LifecycleExperimentFailureCategories.LifecycleMarkerInvalid,
            LifecycleExperimentStage.PluginRemoval => LifecycleExperimentFailureCategories.PluginRemovalFailed,
            LifecycleExperimentStage.LoaderRollback => LifecycleExperimentFailureCategories.LoaderRollbackFailed,
            LifecycleExperimentStage.DisposablePostcheck => LifecycleExperimentFailureCategories.DisposableRestorationFailed,
            LifecycleExperimentStage.OriginalPostcheck => LifecycleExperimentFailureCategories.OriginalPostcheckFailed,
            _ => LifecycleExperimentFailureCategories.StageOperationMissing
        };

    private sealed class LifecycleEvidenceAccumulator
    {
        public string? SelectedExecutableRelativePath { get; private set; }
        public string? LoaderTransactionStatus { get; private set; }
        public string? UnitySourceAssemblyIdentity { get; private set; }
        public string? PackageSha256 { get; private set; }
        public string? AdmissionBindingDigest { get; private set; }
        public int? InitializationCount { get; private set; }
        public int? QuittingCount { get; private set; }
        public int? ShutdownCount { get; private set; }
        public string? MarkerEncounterOrder { get; private set; }
        public string? RuntimeApiIdentity { get; private set; }
        public string? RuntimeContractsIdentity { get; private set; }
        public int? PluginCount { get; private set; }
        public int? WarningCount { get; private set; }
        public int? ErrorCount { get; private set; }
        public int? FatalErrorCount { get; private set; }
        public bool? PluginRemovalVerified { get; private set; }
        public bool? LoaderRollbackVerified { get; private set; }
        public bool? DisposableRestorationVerified { get; private set; }
        public bool? OriginalManifestVerified { get; private set; }
        public bool? OriginalRuntimeVerified { get; private set; }
        public bool? OriginalLoaderIndicatorsAbsent { get; private set; }
        public bool? RecoveryMarkerPersisted { get; private set; }
        public string? RecoveryMarkerFailureCategory { get; private set; }
        public string? RollbackCommand { get; private set; }
        public bool LoaderApplied { get; private set; }
        public bool PluginDeployed { get; private set; }
        public bool ProcessActive { get; private set; }

        public bool HasCompleteSuccessEvidence
            => PluginRemovalVerified == true
                && LoaderRollbackVerified == true
                && DisposableRestorationVerified == true
                && OriginalManifestVerified == true
                && OriginalRuntimeVerified == true
                && OriginalLoaderIndicatorsAbsent == true;

        public void Apply(LifecycleExperimentStage stage, LifecycleStageEvidence evidence)
        {
            LoaderTransactionStatus = evidence.LoaderTransactionStatus ?? LoaderTransactionStatus;
            LoaderApplied |= evidence.LoaderApplied == true;
            PluginDeployed |= evidence.PluginDeployed == true;
            ProcessActive |= evidence.ProcessActive == true;
            if (evidence is OriginalPreflightEvidence preflight)
            {
                SelectedExecutableRelativePath = preflight.SelectedExecutableRelativePath;
            }
            if (evidence is LoaderVerificationEvidence loader)
            {
                LoaderTransactionStatus = loader.LoaderStatus;
            }
            if (evidence is UnityMetadataEvidence unity)
            {
                UnitySourceAssemblyIdentity = unity.SourceAssemblyIdentity;
            }
            if (evidence is PackageEvidence package)
            {
                PackageSha256 = package.PackageSha256;
            }
            if (evidence is DeploymentEvidence deployment)
            {
                PackageSha256 = deployment.PackageSha256 ?? PackageSha256;
                AdmissionBindingDigest = deployment.AdmissionBindingDigest;
            }
            if (evidence is LifecycleVerificationEvidence lifecycle)
            {
                InitializationCount = lifecycle.InitializationCount;
                QuittingCount = lifecycle.QuittingCount;
                ShutdownCount = lifecycle.ShutdownCount;
                MarkerEncounterOrder = lifecycle.MarkerEncounterOrder;
                RuntimeApiIdentity = lifecycle.RuntimeApiIdentity;
                RuntimeContractsIdentity = lifecycle.RuntimeContractsIdentity;
                PluginCount = lifecycle.PluginCount;
                WarningCount = lifecycle.WarningCount;
                ErrorCount = lifecycle.ErrorCount;
                FatalErrorCount = lifecycle.FatalErrorCount;
            }
            if (evidence is CleanupEvidence cleanup)
            {
                if (stage == LifecycleExperimentStage.PluginRemoval)
                {
                    PluginRemovalVerified = cleanup.RemovalVerified;
                }
                if (stage == LifecycleExperimentStage.LoaderRollback)
                {
                    LoaderRollbackVerified = cleanup.RollbackVerified;
                }
            }
            if (evidence is PostcheckEvidence postcheck)
            {
                if (stage == LifecycleExperimentStage.DisposablePostcheck)
                {
                    DisposableRestorationVerified = postcheck.RestorationVerified;
                }
                if (stage == LifecycleExperimentStage.OriginalPostcheck)
                {
                    OriginalManifestVerified = postcheck.ManifestVerified;
                    OriginalRuntimeVerified = postcheck.RuntimeVerified;
                    OriginalLoaderIndicatorsAbsent = postcheck.LoaderIndicatorsAbsent;
                }
            }
            if (evidence is RecoveryEvidence recovery)
            {
                RecoveryMarkerPersisted = recovery.MarkerPersisted;
                RecoveryMarkerFailureCategory = recovery.FailureCategory;
                RollbackCommand = recovery.RollbackCommand;
            }
        }

        public LifecycleExperimentResult ToResult(
            string overall,
            LifecycleExperimentStage current,
            LifecycleExperimentStage? failed,
            LifecycleExperimentStage? lastCompleted,
            string category,
            bool statePersisted,
            LifecycleExperimentStage? primaryFailedStage,
            string? primaryFailureCategory,
            string? cleanupFailureCategory)
            => new(
                overall,
                current,
                failed,
                lastCompleted,
                category,
                statePersisted,
                primaryFailedStage,
                primaryFailureCategory,
                cleanupFailureCategory,
                SelectedExecutableRelativePath,
                LoaderTransactionStatus,
                UnitySourceAssemblyIdentity,
                PackageSha256,
                AdmissionBindingDigest,
                InitializationCount,
                QuittingCount,
                ShutdownCount,
                MarkerEncounterOrder,
                RuntimeApiIdentity,
                RuntimeContractsIdentity,
                PluginCount,
                WarningCount,
                ErrorCount,
                FatalErrorCount,
                PluginRemovalVerified,
                LoaderRollbackVerified,
                DisposableRestorationVerified,
                OriginalManifestVerified,
                OriginalRuntimeVerified,
                OriginalLoaderIndicatorsAbsent,
                RecoveryMarkerPersisted,
                RecoveryMarkerFailureCategory,
                RollbackCommand,
                LoaderApplied,
                PluginDeployed,
                ProcessActive);
    }
}

internal static class LifecycleStageListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> values, T value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(values[index], value))
            {
                return index;
            }
        }

        return -1;
    }
}

namespace ThroneForge.PluginSmokeTest;

public sealed record LifecycleExperimentStageContext(string ExperimentId, string ExpectedFingerprint, LifecycleExperimentStage Stage);

public sealed record LifecycleStageOperationResult(
    bool Succeeded,
    string? FailureCategory = null,
    string? LoaderTransactionStatus = null,
    string? PackageSha256 = null,
    string? AdmissionBindingDigest = null);

public sealed record LifecycleExperimentHooks(
    IReadOnlyDictionary<LifecycleExperimentStage, Func<LifecycleExperimentStageContext, LifecycleStageOperationResult>> Operations)
{
    public static LifecycleExperimentHooks Create(
        IReadOnlyDictionary<LifecycleExperimentStage, Func<LifecycleExperimentStageContext, LifecycleStageOperationResult>> operations)
        => new(operations);

    public static LifecycleExperimentHooks All(Func<LifecycleExperimentStage, LifecycleStageOperationResult> operation)
        => new(Enum.GetValues<LifecycleExperimentStage>()
            .ToDictionary(stage => stage, stage => new Func<LifecycleExperimentStageContext, LifecycleStageOperationResult>(_ => operation(stage))));
}

public sealed record LifecycleExperimentResult(
    string OverallResult,
    LifecycleExperimentStage CurrentStage,
    LifecycleExperimentStage? FailedStage,
    LifecycleExperimentStage? LastCompletedStage,
    string StableCategory,
    bool StageStatePersisted,
    string? SelectedExecutableRelativePath = null,
    string? LoaderTransactionStatus = null,
    string? UnitySourceAssemblyIdentity = null,
    string? PackageSha256 = null,
    string? AdmissionBindingDigest = null,
    int InitializationCount = 0,
    int QuittingCount = 0,
    int ShutdownCount = 0,
    string? MarkerEncounterOrder = null,
    string? RuntimeApiIdentity = null,
    string? RuntimeContractsIdentity = null,
    int PluginCount = 0,
    int WarningCount = 0,
    int ErrorCount = 0,
    int FatalErrorCount = 0,
    bool PluginRemovalVerified = false,
    bool LoaderRollbackVerified = false,
    bool DisposableRestorationVerified = false,
    bool OriginalManifestVerified = false,
    bool OriginalRuntimeVerified = false,
    bool OriginalLoaderIndicatorsAbsent = false);

/// <summary>
/// Repository-testable owner of the lifecycle experiment stage contract.
/// External process and build operations are injected; PowerShell is not a second state machine.
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
    private readonly LifecycleExperimentHooks hooks;

    public LifecycleExperimentOrchestrator(
        string experimentRoot,
        string experimentId,
        string expectedFingerprint,
        LifecycleExperimentHooks hooks)
    {
        this.experimentRoot = experimentRoot;
        this.experimentId = experimentId;
        this.expectedFingerprint = expectedFingerprint;
        this.hooks = hooks;
    }

    public LifecycleExperimentResult Run()
    {
        var lastCompleted = (LifecycleExperimentStage?)null;
        var currentStage = RequiredStages[0];
        var statePersisted = false;
        string? loaderStatus = null;
        string? packageDigest = null;
        string? bindingDigest = null;

        try
        {
            var prepared = LifecycleExperimentStageStateService.CreatePrepared(experimentId, expectedFingerprint);
            LifecycleExperimentStageStateService.SaveAtomic(experimentRoot, prepared);
            statePersisted = true;
        }
        catch (Exception exception) when (exception is PluginSmokeException or IOException or UnauthorizedAccessException)
        {
            return new("Failed", currentStage, currentStage, null, LifecycleExperimentFailureCategories.StageStatePersistenceFailed, false);
        }

        foreach (var stage in RequiredStages)
        {
            currentStage = stage;
            Persist(stage, lastCompleted, LifecycleExperimentFailureCategories.InProgress, ref statePersisted, loaderStatus, packageDigest, bindingDigest);
            if (!hooks.Operations.TryGetValue(stage, out var operation))
            {
                return Fail(currentStage, lastCompleted, LifecycleExperimentFailureCategories.StageOperationMissing, statePersisted);
            }

            LifecycleStageOperationResult result;
            try
            {
                result = operation(new LifecycleExperimentStageContext(experimentId, expectedFingerprint, stage));
            }
            catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
            {
                return Fail(currentStage, lastCompleted, FailureCategoryFor(stage), statePersisted);
            }

            if (!result.Succeeded)
            {
                return Fail(currentStage, lastCompleted, result.FailureCategory ?? FailureCategoryFor(stage), statePersisted);
            }

            loaderStatus = result.LoaderTransactionStatus ?? loaderStatus;
            packageDigest = result.PackageSha256 ?? packageDigest;
            bindingDigest = result.AdmissionBindingDigest ?? bindingDigest;
            lastCompleted = stage;
            var next = RequiredStages.IndexOf(stage) == RequiredStages.Count - 1
                ? LifecycleExperimentStage.Completed
                : RequiredStages[RequiredStages.IndexOf(stage) + 1];
            Persist(next, lastCompleted, LifecycleExperimentFailureCategories.StageCompleted, ref statePersisted, loaderStatus, packageDigest, bindingDigest);
        }

        return new(
            "Passed",
            LifecycleExperimentStage.Completed,
            null,
            lastCompleted,
            LifecycleExperimentFailureCategories.StageCompleted,
            statePersisted,
            LoaderTransactionStatus: loaderStatus,
            PackageSha256: packageDigest,
            AdmissionBindingDigest: bindingDigest,
            OriginalManifestVerified: true,
            OriginalRuntimeVerified: true,
            OriginalLoaderIndicatorsAbsent: true,
            DisposableRestorationVerified: true,
            LoaderRollbackVerified: true,
            PluginRemovalVerified: true);
    }

    private LifecycleExperimentResult Fail(
        LifecycleExperimentStage stage,
        LifecycleExperimentStage? lastCompleted,
        string category,
        bool statePersisted)
    {
        Persist(stage, lastCompleted, category, ref statePersisted, null, null, null);
        return new("Failed", stage, stage, lastCompleted, category, statePersisted);
    }

    private void Persist(
        LifecycleExperimentStage current,
        LifecycleExperimentStage? lastCompleted,
        string category,
        ref bool statePersisted,
        string? loaderStatus,
        string? packageDigest,
        string? bindingDigest)
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
                loaderStatus,
                packageDigest,
                bindingDigest);
            statePersisted = true;
        }
        catch (Exception exception) when (exception is PluginSmokeException or IOException or UnauthorizedAccessException)
        {
            statePersisted = false;
        }
    }

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
            _ => "lifecycle-stage-failed"
        };
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

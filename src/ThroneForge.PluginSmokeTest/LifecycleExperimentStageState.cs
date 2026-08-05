using System.Text;
using System.Text.Json;
using ThroneForge.LoaderSmokeTest;

namespace ThroneForge.PluginSmokeTest;

public enum LifecycleExperimentStage
{
    OriginalPreflight,
    DisposablePrepare,
    BaselineLaunch,
    LoaderInstall,
    LoaderLaunch,
    LoaderVerify,
    UnityMetadataPreflight,
    PackageBuild,
    PackageCapture,
    AdmitAndDeploy,
    LifecycleLaunch,
    LogStability,
    LifecycleVerification,
    PluginRemoval,
    LoaderRollback,
    DisposablePostcheck,
    OriginalPostcheck,
    Completed
}

public static class LifecycleExperimentFailureCategories
{
    public const string InProgress = "in-progress";
    public const string StageCompleted = "stage-completed";
    public const string StageOperationMissing = "stage-operation-missing";
    public const string StageStatePersistenceFailed = "stage-state-persistence-failed";
    public const string OriginalPreflightFailed = "original-preflight-failed";
    public const string ExecutableBindingMismatch = "executable-binding-mismatch";
    public const string DisposablePrepareFailed = "disposable-prepare-failed";
    public const string BaselineLaunchFailed = "baseline-launch-failed";
    public const string LoaderInstallFailed = "loader-install-failed";
    public const string LoaderTransactionMissing = "loader-transaction-missing";
    public const string LoaderLaunchFailed = "loader-launch-failed";
    public const string LoaderVerifyFailed = "loader-verify-failed";
    public const string UnityMetadataPreflightFailed = "unity-metadata-preflight-failed";
    public const string PackageBuildFailed = "package-build-failed";
    public const string PackageCaptureFailed = "package-capture-failed";
    public const string AdmissionFailed = "admission-failed";
    public const string DeploymentFailed = "deployment-failed";
    public const string MetadataValidationFailed = "metadata-validation-failed";
    public const string DeploymentContextFailed = "deployment-context-failed";
    public const string DeploymentWriteFailed = "deployment-write-failed";
    public const string DeploymentVerificationFailed = "deployment-verification-failed";
    public const string LifecycleLaunchFailed = "lifecycle-launch-failed";
    public const string ManualClosureRequired = "manual-closure-required";
    public const string ProcessActive = PluginSmokeStateFailureCategories.ProcessActive;
    public const string LogMissing = "log-missing";
    public const string LogNotReadable = "log-not-readable";
    public const string LogNotStable = "log-not-stable";
    public const string LifecycleMarkerMissing = "lifecycle-marker-missing";
    public const string LifecycleMarkerInvalid = "lifecycle-marker-invalid";
    public const string PluginRemovalFailed = "plugin-removal-failed";
    public const string LoaderRollbackFailed = "loader-rollback-failed";
    public const string DisposableRestorationFailed = "disposable-restoration-failed";
    public const string OriginalPostcheckFailed = "original-postcheck-failed";
    public const string CleanupFailed = "cleanup-failed";
    public const string OwnershipStateInvalid = PluginSmokeStateFailureCategories.OwnershipStateInvalid;
    public const string BaselineStateMissing = PluginSmokeStateFailureCategories.BaselineStateMissing;
    public const string BaselineStateMismatch = PluginSmokeStateFailureCategories.BaselineStateMismatch;
    public const string TransactionStateMissing = PluginSmokeStateFailureCategories.TransactionStateMissing;
    public const string TransactionStateMismatch = PluginSmokeStateFailureCategories.TransactionStateMismatch;
    public const string AppliedProfileDrift = PluginSmokeStateFailureCategories.AppliedProfileDrift;
    public const string BootstrapEvidenceInvalid = PluginSmokeStateFailureCategories.BootstrapEvidenceInvalid;
    public const string RecoveryOwnershipInvalid = "recovery-ownership-invalid";
    public const string RecoveryTransactionMissing = "recovery-transaction-missing";
    public const string RecoveryTransactionMismatch = "recovery-transaction-mismatch";
    public const string RecoveryProcessActive = "recovery-process-active";
    public const string RecoveryRuntimeDrift = "recovery-runtime-drift";
    public const string RecoveryPluginRemovalFailed = "recovery-plugin-removal-failed";
    public const string RecoveryLoaderRollbackFailed = "recovery-loader-rollback-failed";
    public const string RecoveryBaselineRestoreFailed = "recovery-baseline-restore-failed";
    public const string RecoveryDisposableReadinessFailed = "recovery-disposable-readiness-failed";
    public const string RecoveryOriginalPostcheckFailed = "recovery-original-postcheck-failed";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        InProgress, StageCompleted, StageOperationMissing, StageStatePersistenceFailed,
        OriginalPreflightFailed, ExecutableBindingMismatch, DisposablePrepareFailed, BaselineLaunchFailed,
        LoaderInstallFailed, LoaderTransactionMissing, LoaderLaunchFailed, LoaderVerifyFailed,
        UnityMetadataPreflightFailed, PackageBuildFailed, PackageCaptureFailed, AdmissionFailed,
        DeploymentFailed, MetadataValidationFailed, DeploymentContextFailed, DeploymentWriteFailed,
        DeploymentVerificationFailed, LifecycleLaunchFailed, ManualClosureRequired, LogMissing,
        LogNotReadable, LogNotStable, LifecycleMarkerMissing, LifecycleMarkerInvalid,
        PluginRemovalFailed, LoaderRollbackFailed, DisposableRestorationFailed,
        OriginalPostcheckFailed, CleanupFailed,
        PluginSmokeStateFailureCategories.OwnershipStateInvalid,
        PluginSmokeStateFailureCategories.BaselineStateMissing,
        PluginSmokeStateFailureCategories.BaselineStateMismatch,
        PluginSmokeStateFailureCategories.TransactionStateMissing,
        PluginSmokeStateFailureCategories.TransactionStateMismatch,
        PluginSmokeStateFailureCategories.AppliedProfileDrift,
        PluginSmokeStateFailureCategories.BootstrapEvidenceInvalid,
        RecoveryOwnershipInvalid, RecoveryTransactionMissing, RecoveryTransactionMismatch,
        RecoveryProcessActive, RecoveryRuntimeDrift, RecoveryPluginRemovalFailed,
        RecoveryLoaderRollbackFailed, RecoveryBaselineRestoreFailed,
        RecoveryDisposableReadinessFailed, RecoveryOriginalPostcheckFailed
    };
}

public sealed record LifecycleExperimentStageState(
    string SchemaVersion,
    string TaskVersion,
    string ExperimentId,
    string ExpectedFingerprint,
    LifecycleExperimentStage CurrentStage,
    LifecycleExperimentStage? LastCompletedStage,
    string ResultCategory,
    string? LoaderTransactionStatus = null,
    string? PackageSha256 = null,
    string? AdmissionBindingDigest = null,
    LifecycleExperimentStage? PrimaryFailedStage = null,
    string? PrimaryFailureCategory = null,
    string? CleanupFailureCategory = null);

public static class LifecycleExperimentStageStateService
{
    public const string SchemaVersion = "throneforge-task7-lifecycle-stage-v2";
    public const string TaskVersion = "m1-lifecycle-binding-smoke-test-v1";
    public const string StateRelativePath = "evidence/lifecycle-stage-state.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static LifecycleExperimentStageState CreatePrepared(string experimentId, string expectedFingerprint)
    {
        ValidateExperimentId(experimentId);
        ValidateFingerprint(expectedFingerprint);
        return new(
            SchemaVersion,
            TaskVersion,
            experimentId,
            expectedFingerprint.ToLowerInvariant(),
            LifecycleExperimentStage.OriginalPreflight,
            null,
            LifecycleExperimentFailureCategories.InProgress);
    }

    public static string GetStatePath(string experimentRoot)
        => Path.GetFullPath(Path.Combine(experimentRoot, StateRelativePath.Replace('/', Path.DirectorySeparatorChar)));

    public static void SaveAtomic(string experimentRoot, LifecycleExperimentStageState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Validate(state);
        var root = Path.GetFullPath(experimentRoot);
        var path = GetStatePath(root);
        SmokeTestPathValidator.EnsureWithin(root, path);
        SmokeTestPathValidator.EnsureNoReparsePointsOnPath(root);
        var parent = Path.GetDirectoryName(path) ?? throw new PluginSmokeException("The lifecycle stage state has no safe parent.");
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeException("The lifecycle stage state could not be written safely.", exception);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static LifecycleExperimentStageState LoadAndValidate(string experimentRoot, string expectedExperimentId, string expectedFingerprint)
    {
        ValidateExperimentId(expectedExperimentId);
        ValidateFingerprint(expectedFingerprint);
        var root = Path.GetFullPath(experimentRoot);
        var path = GetStatePath(root);
        SmokeTestPathValidator.EnsureWithin(root, path);
        SmokeTestPathValidator.EnsureNoReparsePointsOnPath(root);
        if (!File.Exists(path))
        {
            throw new PluginSmokeException("The lifecycle experiment stage state is missing.");
        }

        try
        {
            var state = JsonSerializer.Deserialize<LifecycleExperimentStageState>(File.ReadAllText(path))
                ?? throw new PluginSmokeException("The lifecycle experiment stage state is empty or malformed.");
            Validate(state);
            if (!state.ExperimentId.Equals(expectedExperimentId, StringComparison.Ordinal)
                || !state.ExpectedFingerprint.Equals(expectedFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new PluginSmokeException("The lifecycle experiment stage state is bound to different evidence.");
            }

            return state;
        }
        catch (PluginSmokeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            throw new PluginSmokeException("The lifecycle experiment stage state is missing or malformed.", exception);
        }
    }

    public static LifecycleExperimentStageState Advance(
        string experimentRoot,
        string experimentId,
        string expectedFingerprint,
        LifecycleExperimentStage currentStage,
        LifecycleExperimentStage? lastCompletedStage,
        string resultCategory,
        string? loaderTransactionStatus = null,
        string? packageSha256 = null,
        string? admissionBindingDigest = null,
        LifecycleExperimentStage? primaryFailedStage = null,
        string? primaryFailureCategory = null,
        string? cleanupFailureCategory = null)
    {
        var existing = File.Exists(GetStatePath(experimentRoot))
            ? LoadAndValidate(experimentRoot, experimentId, expectedFingerprint)
            : null;
        var primaryStage = existing?.PrimaryFailedStage ?? primaryFailedStage;
        var primaryCategory = existing?.PrimaryFailureCategory ?? primaryFailureCategory;
        var cleanupCategory = cleanupFailureCategory ?? existing?.CleanupFailureCategory;
        var state = new LifecycleExperimentStageState(
            SchemaVersion,
            TaskVersion,
            experimentId,
            expectedFingerprint.ToLowerInvariant(),
            currentStage,
            lastCompletedStage,
            resultCategory,
            loaderTransactionStatus ?? existing?.LoaderTransactionStatus,
            packageSha256 ?? existing?.PackageSha256,
            admissionBindingDigest ?? existing?.AdmissionBindingDigest,
            primaryStage,
            primaryCategory,
            cleanupCategory);
        SaveAtomic(experimentRoot, state);
        return state;
    }

    private static void Validate(LifecycleExperimentStageState state)
    {
        if (!string.Equals(state.SchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(state.TaskVersion, TaskVersion, StringComparison.Ordinal)
            || !Enum.IsDefined(state.CurrentStage)
            || state.LastCompletedStage is not null && !Enum.IsDefined(state.LastCompletedStage.Value)
            || state.PrimaryFailedStage is not null && !Enum.IsDefined(state.PrimaryFailedStage.Value)
            || !LifecycleExperimentFailureCategories.All.Contains(state.ResultCategory)
            || state.PrimaryFailureCategory is not null && !LifecycleExperimentFailureCategories.All.Contains(state.PrimaryFailureCategory)
            || state.CleanupFailureCategory is not null && !LifecycleExperimentFailureCategories.All.Contains(state.CleanupFailureCategory))
        {
            throw new PluginSmokeException("The lifecycle experiment stage state has an unsupported schema, stage, or result category.");
        }

        ValidateExperimentId(state.ExperimentId);
        ValidateFingerprint(state.ExpectedFingerprint);
        ValidateOptionalHash(state.PackageSha256, "package");
        ValidateOptionalHash(state.AdmissionBindingDigest, "admission");
        ValidateOptionalToken(state.LoaderTransactionStatus, "loader transaction status");
    }

    private static void ValidateExperimentId(string value)
    {
        if (!Guid.TryParseExact(value, "N", out _))
        {
            throw new PluginSmokeException("The lifecycle experiment identity is invalid.");
        }
    }

    private static void ValidateFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new PluginSmokeException("The lifecycle fingerprint must be a 64-character SHA-256 value.");
        }
    }

    private static void ValidateOptionalHash(string? value, string label)
    {
        if (value is not null && (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new PluginSmokeException($"The lifecycle {label} digest is invalid.");
        }
    }

    private static void ValidateOptionalToken(string? value, string label)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '/' or '\\' or ':')))
        {
            throw new PluginSmokeException($"The lifecycle {label} is invalid.");
        }
    }
}

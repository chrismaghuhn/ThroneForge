namespace ThroneForge.LoaderSmokeTest;

public enum SmokeTestOutcome
{
    Passed,
    PassedWithWarnings,
    Failed,
    Inconclusive
}

public enum SmokeTestMode
{
    Plan,
    Prepare,
    Baseline,
    Install,
    Launch,
    Verify,
    Rollback,
    Full,
    Resume,
    Cleanup
}

public enum ManifestVerificationStatus
{
    Matches,
    AddedFiles,
    RemovedFiles,
    ChangedFiles
}

public enum SmokeTestRollbackState
{
    NotApplied,
    RollbackSucceeded,
    RollbackFailed,
    ManualClosureRequired
}

public enum OriginalInstallationVerificationState
{
    PreflightPassedPostCheckPending,
    NoTransactionApplied,
    CompletePostCheckPassed,
    CompletePostCheckFailed,
    ManualClosureDeferred
}

public sealed record OriginalInstallationVerificationEvidence(
    OriginalInstallationVerificationState State,
    IReadOnlyList<string> FailedCategories)
{
    public string ToReportText()
        => State switch
        {
            OriginalInstallationVerificationState.PreflightPassedPostCheckPending
                => "Preflight passed; complete original post-verification is pending.",
            OriginalInstallationVerificationState.NoTransactionApplied
                => "Preflight passed; complete post-verification was not required because no loader transaction was applied.",
            OriginalInstallationVerificationState.CompletePostCheckPassed
                => "Preflight and complete original post-verification passed; all required compatibility and integrity checks matched.",
            OriginalInstallationVerificationState.CompletePostCheckFailed
                => "Preflight passed; complete original post-verification failed: "
                    + (FailedCategories.Count == 0 ? "unspecified check." : string.Join(", ", FailedCategories) + "."),
            OriginalInstallationVerificationState.ManualClosureDeferred
                => "Preflight passed; complete original post-verification was deferred because manual closure is required.",
            _ => "Original installation post-verification state is unknown."
        };
}

public enum TransactionChangeKind
{
    NewFile,
    Overwrite,
    Unchanged,
    CreatedDirectory
}

public enum LoaderTransactionStatus
{
    Prepared,
    Applied,
    LaunchObserved,
    RollbackRequired,
    RolledBack,
    FailedAndRolledBack
}

public sealed class SmokeTestException : Exception
{
    public SmokeTestException(string message)
        : base(message)
    {
    }

    public SmokeTestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed record SmokeTestRoots(
    string RepositoryRoot,
    string OriginalGameRoot,
    string ExperimentRoot,
    string CleanGameRoot,
    string DownloadsRoot,
    string ExtractedLoaderRoot,
    string EvidenceRoot,
    string ManifestsRoot,
    string BackupRoot);

public sealed record FileManifestEntry(string RelativePath, long Size, string Sha256);

public sealed record CopyManifest(
    IReadOnlyList<FileManifestEntry> Files,
    IReadOnlyList<string>? Directories = null);

public sealed record ManifestDifference(
    string RelativePath,
    FileManifestEntry? Expected,
    FileManifestEntry? Actual);

public sealed record ManifestVerificationResult(
    ManifestVerificationStatus Status,
    IReadOnlyList<ManifestDifference> AddedFiles,
    IReadOnlyList<ManifestDifference> RemovedFiles,
    IReadOnlyList<ManifestDifference> ChangedFiles,
    IReadOnlyList<string> UnexpectedDirectories,
    IReadOnlyList<string> MissingDirectories)
{
    public bool Matches => Status == ManifestVerificationStatus.Matches
        && AddedFiles.Count == 0
        && RemovedFiles.Count == 0
        && ChangedFiles.Count == 0
        && UnexpectedDirectories.Count == 0
        && MissingDirectories.Count == 0;
}

public sealed record DisposableProfileBaseline(
    string SchemaVersion,
    string TaskVersion,
    string ExpectedOriginalFingerprint,
    CopyManifest OriginalManifest,
    CopyManifest DisposableManifest);

public sealed record ArchiveSafetyLimits(
    int MaximumEntries = 4096,
    long MaximumExpandedBytes = 512 * 1024 * 1024,
    int MaximumPathLength = 240);

public sealed record ArchiveManifestEntry(
    string RelativePath,
    bool IsDirectory,
    long Size,
    string Sha256);

public sealed record ArchiveInspectionResult(
    string ArchivePath,
    IReadOnlyList<ArchiveManifestEntry> Manifest,
    long ExpandedBytes,
    string? ExtractionRoot = null);

public sealed record TransactionEntry(
    string RelativePath,
    TransactionChangeKind Change,
    string? OriginalSha256,
    string? ReplacementSha256,
    string? BackupRelativePath);

public sealed record TransactionPlan(
    string ExtractionRoot,
    IReadOnlyList<TransactionEntry> Entries);

public sealed record LoaderBootstrapEvidence(
    string? BepInExVersion,
    bool PreloaderInitialized,
    bool ChainloaderInitialized,
    int PluginsDiscovered,
    int WarningCount,
    int ErrorCount,
    int FatalErrorCount)
{
    public bool MeetsBootstrapCriteria
        => string.Equals(BepInExVersion, "5.4.23.5", StringComparison.Ordinal)
            && PreloaderInitialized
            && ChainloaderInitialized
            && PluginsDiscovered == 0
            && FatalErrorCount == 0;
}

public sealed record LoaderTransactionState(
    string SchemaVersion,
    string TaskVersion,
    string ExpectedFingerprint,
    string BaselineManifestIdentity,
    string ArchiveName,
    string ObservedArchiveSha256,
    LoaderTransactionStatus Status,
    CopyManifest ExpectedAppliedManifest,
    IReadOnlyList<TransactionEntry> Entries,
    IReadOnlyList<FileManifestEntry> GeneratedEvidenceFiles,
    IReadOnlyList<string>? GeneratedEvidenceDirectories = null,
    LoaderBootstrapEvidence? LaunchEvidence = null);

public sealed record LaunchObservationResult(
    bool Started,
    bool StableInitialized,
    bool Exited,
    int? ExitCode,
    bool ExecutableWasInsideExperiment,
    bool RequiresManualClosure,
    TimeSpan Elapsed,
    string FailureCategory);

public sealed record LoaderLogSummary(
    string? BepInExVersion,
    bool ConfigurationGenerated,
    bool PreloaderInitialized,
    bool ChainloaderInitialized,
    int PluginsDiscovered,
    int WarningCount,
    int ErrorCount,
    int FatalErrorCount,
    IReadOnlyList<string> ErrorCategories,
    bool StableInitialized);

public sealed record SmokeTestPostApplyResult(
    SmokeTestOutcome Outcome,
    SmokeTestRollbackState RollbackState,
    LaunchObservationResult? Launch,
    LoaderLogSummary? LogSummary,
    string FailureCategory,
    Exception? OperationException = null,
    Exception? RollbackException = null,
    bool RecoveryMarkerPersisted = false,
    string? RecoveryMarkerFailureCategory = null);

public sealed record SmokeTestExecutionHooks(
    Func<SmokeTestRoots, string, LaunchObservationResult>? Launch = null,
    Func<string, string>? ReadLoaderLog = null,
    Func<string, LoaderLogSummary>? ParseLoaderLog = null,
    Func<SmokeTestDetailedReport, string>? BuildReport = null,
    Func<string, string, bool, string>? WriteReport = null);

public sealed record SmokeTestReportData(
    string Fingerprint,
    string OriginalGamePath,
    string ExperimentPath,
    SmokeTestOutcome Outcome,
    string RawLog,
    string? BepInExVersion,
    string TaskVersion = "m1-loader-smoke-test-v1");

public sealed record SmokeTestDetailedReport(
    string Fingerprint,
    string TaskVersion,
    DateTimeOffset TimestampUtc,
    SmokeTestOutcome Outcome,
    string OriginalInstallationVerification,
    string DisposableProfileVerification,
    string BaselineLaunchResult,
    string OfficialReleaseVerification,
    string ArchiveAssetName,
    string ArchiveAssetId,
    string ArchiveSize,
    string ArchiveDigestStatus,
    string ObservedSha256,
    string SecureExtractionResult,
    string TransactionSummary,
    string LoaderEnabledLaunchResult,
    string GeneratedBepInExEvidence,
    LoaderLogSummary LogSummary,
    string RollbackResult,
    string OriginalPostVerification,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    string RemainingUncertainty,
    string NextPermittedTask,
    string OriginalFullManifestPostVerification = "Not performed.",
    string OriginalRuntimeReadinessPostVerification = "Not performed.",
    string OriginalLoaderIndicatorPostVerification = "Not performed.",
    string DisposableFullManifestRollbackVerification = "Not performed.",
    string RecoveryOrRollbackState = "Not recorded.");

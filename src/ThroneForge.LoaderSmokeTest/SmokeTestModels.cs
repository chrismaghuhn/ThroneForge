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

public sealed record LoaderLogReadEvidence(
    bool Present,
    bool Readable,
    string? FailureCategory = null);

public static class LoaderLaunchDiagnosticCategories
{
    public const string Unknown = "unknown";
    public const string BootstrapObserved = "bootstrap-observed";
    public const string BootstrapEvidenceInvalid = "bootstrap-evidence-invalid";
    public const string LogMissing = "log-missing";
    public const string LogNotReadable = "log-not-readable";
    public const string ManualClosureRequired = "manual-closure-required";
    public const string PreloaderNotInitialized = "preloader-not-initialized";
    public const string ChainloaderNotInitialized = "chainloader-not-initialized";
    public const string BepInExVersionMismatch = "bepinex-version-mismatch";
    public const string UnexpectedPluginCount = "unexpected-plugin-count";
    public const string FatalLoaderError = "fatal-loader-error";
}

public sealed record LoaderLaunchDiagnosticEvidence(
    bool ProcessStarted,
    bool ProcessExited,
    bool ExecutableInsideExperiment,
    bool RequiresManualClosure,
    int? ExitCode,
    string LaunchCategory,
    bool LogPresent,
    bool LogReadable,
    string? BepInExVersion,
    bool PreloaderInitialized,
    bool ChainloaderInitialized,
    int PluginsDiscovered,
    int WarningCount,
    int ErrorCount,
    int FatalErrorCount,
    string DiagnosticCategory,
    bool BootstrapObserved)
{
    public static LoaderLaunchDiagnosticEvidence Create(
        LaunchObservationResult launch,
        LoaderLogReadEvidence log,
        LoaderLogSummary? summary,
        bool bootstrapObserved)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(log);
        var safeLaunchCategory = SafeCategory(launch.FailureCategory);
        var diagnosticCategory = launch.RequiresManualClosure
            ? LoaderLaunchDiagnosticCategories.ManualClosureRequired
            : !launch.Started
                ? safeLaunchCategory
                : !log.Present
                    ? LoaderLaunchDiagnosticCategories.LogMissing
                    : !log.Readable
                        ? LoaderLaunchDiagnosticCategories.LogNotReadable
                        : summary is not null && !summary.PreloaderInitialized
                            ? LoaderLaunchDiagnosticCategories.PreloaderNotInitialized
                            : summary is not null && !summary.ChainloaderInitialized
                                ? LoaderLaunchDiagnosticCategories.ChainloaderNotInitialized
                                : summary is not null && !string.Equals(summary.BepInExVersion, "5.4.23.5", StringComparison.Ordinal)
                                    ? LoaderLaunchDiagnosticCategories.BepInExVersionMismatch
                                    : summary is not null && summary.PluginsDiscovered != 0
                                        ? LoaderLaunchDiagnosticCategories.UnexpectedPluginCount
                                        : summary is not null && summary.FatalErrorCount != 0
                                            ? LoaderLaunchDiagnosticCategories.FatalLoaderError
                                            : !bootstrapObserved
                                                ? LoaderLaunchDiagnosticCategories.BootstrapEvidenceInvalid
                                                : LoaderLaunchDiagnosticCategories.BootstrapObserved;
        return new(
            launch.Started,
            launch.Exited,
            launch.ExecutableWasInsideExperiment,
            launch.RequiresManualClosure,
            launch.ExitCode,
            safeLaunchCategory,
            log.Present,
            log.Readable,
            summary?.BepInExVersion,
            summary?.PreloaderInitialized == true,
            summary?.ChainloaderInitialized == true,
            summary?.PluginsDiscovered ?? 0,
            summary?.WarningCount ?? 0,
            summary?.ErrorCount ?? 0,
            summary?.FatalErrorCount ?? 0,
            diagnosticCategory,
            bootstrapObserved);
    }

    private static string SafeCategory(string? value)
        => string.IsNullOrWhiteSpace(value)
            || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '\\' or '/' or ':')
            ? LoaderLaunchDiagnosticCategories.Unknown
            : value;
}

public enum RollbackDriftStatus
{
    Matches,
    ApprovedGeneratedDifferencesOnly,
    UnapprovedDifferences
}

public sealed record RollbackDriftDifference(
    string Kind,
    string RelativePath,
    bool IsApprovedGeneratedEvidence);

public sealed record RollbackDriftEvidence(
    RollbackDriftStatus Status,
    IReadOnlyList<RollbackDriftDifference> Differences,
    int TotalDifferenceCount,
    bool Truncated)
{
    public bool HasUnapprovedDifferences => Status == RollbackDriftStatus.UnapprovedDifferences;
}

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

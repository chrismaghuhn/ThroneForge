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
    Cleanup
}

public enum TransactionChangeKind
{
    NewFile,
    Overwrite,
    Unchanged,
    CreatedDirectory
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

public sealed record CopyManifest(IReadOnlyList<FileManifestEntry> Files);

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
    string NextPermittedTask);

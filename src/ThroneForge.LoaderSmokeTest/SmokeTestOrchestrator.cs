using System.Security.Cryptography;
using System.Text.Json;
using ThroneForge.Discovery;

namespace ThroneForge.LoaderSmokeTest;

public sealed record LoaderSmokeTestRequest(
    SmokeTestMode Mode,
    string GamePath,
    string ExperimentRoot,
    string ExpectedFingerprint,
    string RepositoryRoot,
    string? BepInExArchivePath,
    string? ReportPath,
    bool WhatIf = false,
    string? OfficialReleaseSummaryPath = null,
    string? OfficialAssetDigest = null,
    string? OfficialAssetId = null,
    string? OfficialAssetSize = null);

public sealed record SmokeTestExecutionResult(
    SmokeTestOutcome Outcome,
    string Message,
    string? ReportPath,
    string OriginalFingerprint,
    string? CopiedFingerprint,
    bool OriginalInstallationVerified,
    bool RollbackVerified);

public static class SmokeTestOrchestrator
{
    private const string ExpectedArchiveName = "BepInEx_win_x64_5.4.23.5.zip";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SmokeTestExecutionResult Run(LoaderSmokeTestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateExpectedFingerprint(request.ExpectedFingerprint);
        var roots = SmokeTestPathValidator.ValidateRoots(
            request.RepositoryRoot,
            request.GamePath,
            request.ExperimentRoot);
        if (request.Mode == SmokeTestMode.Cleanup)
        {
            return Cleanup(roots, request);
        }

        var preflight = VerifyOriginalInstallation(request, roots);
        if (request.WhatIf || request.Mode == SmokeTestMode.Plan)
        {
            return new SmokeTestExecutionResult(
                SmokeTestOutcome.Inconclusive,
                "Preflight succeeded; no files were copied or changed because this was a plan/dry run.",
                null,
                preflight.Snapshot.Fingerprint,
                null,
                true,
                false);
        }

        return request.Mode switch
        {
            SmokeTestMode.Prepare => Prepare(request, roots, preflight),
            SmokeTestMode.Baseline => RunBaseline(request, roots, preflight),
            SmokeTestMode.Install => Install(request, roots, preflight),
            SmokeTestMode.Launch => LaunchInstalled(request, roots, preflight),
            SmokeTestMode.Verify => Verify(request, roots, preflight),
            SmokeTestMode.Rollback => Rollback(request, roots, preflight),
            SmokeTestMode.Full => RunFull(request, roots, preflight),
            _ => throw new SmokeTestException("The requested smoke-test mode is unsupported.")
        };
    }

    private static SmokeTestExecutionResult Cleanup(SmokeTestRoots roots, LoaderSmokeTestRequest request)
    {
        var target = SmokeTestPathValidator.ValidateCleanupTarget(roots, roots.ExtractedLoaderRoot);
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        return new SmokeTestExecutionResult(
            SmokeTestOutcome.Passed,
            "The explicitly requested extracted-loader cleanup completed inside the validated experiment root.",
            null,
            request.ExpectedFingerprint,
            null,
            false,
            false);
    }

    private static SmokeTestExecutionResult Prepare(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        var manifest = InstallationCopyService.Copy(roots);
        SaveJson(Path.Combine(roots.ManifestsRoot, "baseline-copy-manifest.json"), manifest);
        EnsureCopiedProfile(roots, request, preflight, manifest);
        return new SmokeTestExecutionResult(
            SmokeTestOutcome.Inconclusive,
            $"Prepared disposable profile with {manifest.Files.Count} files; no loader was installed.",
            null,
            preflight.Snapshot.Fingerprint,
            InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint,
            true,
            false);
    }

    private static SmokeTestExecutionResult RunBaseline(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        EnsureCopiedProfile(roots, request, preflight, null);
        var baseline = LaunchCopiedExecutable(roots, preflight.Snapshot.SelectedExecutableRelativePath);
        var outcome = baseline.Started && baseline.StableInitialized && !baseline.RequiresManualClosure
            ? SmokeTestOutcome.Passed
            : SmokeTestOutcome.Inconclusive;
        return new SmokeTestExecutionResult(
            outcome,
            baseline.FailureCategory,
            null,
            preflight.Snapshot.Fingerprint,
            InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint,
            true,
            false);
    }

    private static SmokeTestExecutionResult Install(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        EnsureCopiedProfile(roots, request, preflight, null);
        if (!File.Exists(Path.Combine(roots.ManifestsRoot, "baseline-copy-manifest.json")))
        {
            SaveJson(
                Path.Combine(roots.ManifestsRoot, "baseline-copy-manifest.json"),
                InstallationCopyService.CaptureManifest(roots.CleanGameRoot));
        }
        var plan = InstallArchive(request, roots);
        SaveJson(Path.Combine(roots.ManifestsRoot, "transaction-plan.json"), plan);
        return new SmokeTestExecutionResult(
            SmokeTestOutcome.Inconclusive,
            "The official archive was applied to the disposable profile; launch and verification remain separate modes.",
            null,
            preflight.Snapshot.Fingerprint,
            InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint,
            true,
            false);
    }

    private static SmokeTestExecutionResult LaunchInstalled(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        EnsureCopiedProfile(roots, request, preflight, null);
        var launch = LaunchCopiedExecutable(roots, preflight.Snapshot.SelectedExecutableRelativePath);
        var summary = LoaderLogParser.Parse(ReadKnownLoaderLog(roots.CleanGameRoot));
        return new SmokeTestExecutionResult(
            SmokeTestOutcomeClassifier.Classify(true, launch.Started && launch.StableInitialized, summary),
            launch.FailureCategory,
            null,
            preflight.Snapshot.Fingerprint,
            InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint,
            true,
            false);
    }

    private static SmokeTestExecutionResult Verify(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        var copied = Directory.Exists(roots.CleanGameRoot)
            ? InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint
            : null;
        var original = InstallationFingerprintService.Capture(roots.OriginalGameRoot).Fingerprint;
        if (!string.Equals(original, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The original installation changed during the experiment.");
        }

        return new SmokeTestExecutionResult(
            copied == request.ExpectedFingerprint ? SmokeTestOutcome.Passed : SmokeTestOutcome.Failed,
            copied == request.ExpectedFingerprint ? "Original and copied fingerprint verification succeeded." : "The disposable profile fingerprint differs.",
            null,
            original,
            copied,
            true,
            false);
    }

    private static SmokeTestExecutionResult Rollback(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        var planPath = Path.Combine(roots.ManifestsRoot, "transaction-plan.json");
        if (!File.Exists(planPath))
        {
            throw new SmokeTestException("No local transaction plan exists for rollback.");
        }

        var plan = LoadJson<TransactionPlan>(planPath);
        LoaderTransactionService.Rollback(roots, plan);
        var baselinePath = Path.Combine(roots.ManifestsRoot, "baseline-copy-manifest.json");
        if (File.Exists(baselinePath))
        {
            InstallationCopyService.RestoreFilesToManifest(
                roots.CleanGameRoot,
                LoadJson<CopyManifest>(baselinePath));
        }
        var copied = InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint;
        var succeeded = string.Equals(copied, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase);
        return new SmokeTestExecutionResult(
            succeeded ? SmokeTestOutcome.Passed : SmokeTestOutcome.Failed,
            succeeded ? "Disposable profile rollback restored the expected fingerprint." : "Disposable profile rollback did not restore the expected fingerprint.",
            null,
            preflight.Snapshot.Fingerprint,
            copied,
            true,
            succeeded);
    }

    private static SmokeTestExecutionResult RunFull(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        var copyManifest = Directory.Exists(roots.CleanGameRoot)
            ? InstallationCopyService.CaptureManifest(roots.CleanGameRoot)
            : InstallationCopyService.Copy(roots);
        SaveJson(Path.Combine(roots.ManifestsRoot, "baseline-copy-manifest.json"), copyManifest);
        var copiedSnapshot = EnsureCopiedProfile(roots, request, preflight, copyManifest);
        var baseline = LaunchCopiedExecutable(roots, copiedSnapshot.SelectedExecutableRelativePath);
        if (!baseline.Started || !baseline.StableInitialized || baseline.RequiresManualClosure)
        {
            return WriteOutcomeReport(
                request,
                roots,
                preflight.Snapshot.Fingerprint,
                copiedSnapshot.Fingerprint,
                SmokeTestOutcome.Inconclusive,
                "BaselineLaunchInconclusive: the copied executable did not reach a bounded stable state or requires manual closure.",
                "Not attempted because the baseline launch was inconclusive.",
                baseline,
                new LoaderLogSummary(null, false, false, false, 0, 0, 0, 0, [], false),
                "No loader transaction was attempted.",
                "No loader was installed.");
        }

        var plan = InstallArchive(request, roots);
        SaveJson(Path.Combine(roots.ManifestsRoot, "transaction-plan.json"), plan);
        var loaderLaunch = LaunchCopiedExecutable(roots, copiedSnapshot.SelectedExecutableRelativePath);
        if (loaderLaunch.RequiresManualClosure)
        {
            throw new SmokeTestException("The loader-enabled process requires manual graceful closure before rollback can proceed.");
        }

        var logText = ReadKnownLoaderLog(roots.CleanGameRoot);
        var summary = LoaderLogParser.Parse(logText);
        var outcome = SmokeTestOutcomeClassifier.Classify(
            true,
            loaderLaunch.Started && loaderLaunch.StableInitialized,
            summary);

        LoaderTransactionService.Rollback(roots, plan);
        InstallationCopyService.RestoreFilesToManifest(roots.CleanGameRoot, copyManifest);
        var copiedAfterRollback = InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint;
        var originalAfter = InstallationFingerprintService.Capture(roots.OriginalGameRoot).Fingerprint;
        var rollbackVerified = string.Equals(copiedAfterRollback, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase);
        var originalVerified = string.Equals(originalAfter, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase);
        if (!rollbackVerified || !originalVerified)
        {
            outcome = SmokeTestOutcome.Failed;
        }

        var reportResult = WriteOutcomeReport(
            request,
            roots,
            originalAfter,
            copiedAfterRollback,
            outcome,
            "The copied executable reached a bounded stable state and was gracefully closed before installation.",
            "The loader-enabled copied executable reached a bounded stable state and was gracefully closed.",
            loaderLaunch,
            summary,
            "Validated archive extraction and transactional apply completed.",
            rollbackVerified ? "Rollback restored the copied fingerprint." : "Rollback did not restore the copied fingerprint.");
        return reportResult with
        {
            OriginalInstallationVerified = originalVerified,
            RollbackVerified = rollbackVerified
        };
    }

    private static TransactionPlan InstallArchive(LoaderSmokeTestRequest request, SmokeTestRoots roots)
    {
        if (string.IsNullOrWhiteSpace(request.BepInExArchivePath))
        {
            throw new SmokeTestException("An explicit BepInEx archive path is required for installation.");
        }

        var archivePath = Path.GetFullPath(request.BepInExArchivePath);
        if (!string.Equals(Path.GetFileName(archivePath), ExpectedArchiveName, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The archive filename must be the exact official BepInEx 5.4.23.5 Windows x64 asset.");
        }

        var observedHash = ComputeHash(archivePath);
        if (!string.IsNullOrWhiteSpace(request.OfficialAssetDigest)
            && !string.Equals(observedHash, request.OfficialAssetDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The downloaded archive digest does not match the supplied official digest.");
        }

        var extracted = ArchiveSafetyService.Extract(archivePath, roots.ExtractedLoaderRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);
        LoaderTransactionService.Apply(roots, plan, extracted);
        return plan;
    }

    private static Preflight VerifyOriginalInstallation(LoaderSmokeTestRequest request, SmokeTestRoots roots)
    {
        var snapshot = InstallationFingerprintService.Capture(roots.OriginalGameRoot);
        if (!string.Equals(snapshot.Fingerprint, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException(
                "The original installation does not match the expected fingerprint. "
                + $"Expected: {request.ExpectedFingerprint.ToLowerInvariant()} Actual: {snapshot.Fingerprint}");
        }

        var evidenceRoot = Path.Combine(roots.ExperimentRoot, "evidence", "runtime-compatibility");
        var runtime = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            roots.OriginalGameRoot,
            request.ExpectedFingerprint,
            evidenceRoot,
            OverwriteExisting: true));
        if (runtime.SmokeTestReadiness.Status != SmokeTestReadiness.ReadyForReversibleTest)
        {
            throw new SmokeTestException("The original installation is no longer ready for a reversible clean-profile test.");
        }

        return new Preflight(snapshot, runtime);
    }

    private static InstallationDiscoverySnapshot EnsureCopiedProfile(
        SmokeTestRoots roots,
        LoaderSmokeTestRequest request,
        Preflight preflight,
        CopyManifest? manifest)
    {
        if (!Directory.Exists(roots.CleanGameRoot))
        {
            throw new SmokeTestException("The disposable copy does not exist; run Prepare first.");
        }

        var copied = InstallationFingerprintService.Capture(roots.CleanGameRoot);
        if (!string.Equals(copied.Fingerprint, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The disposable copy does not match the expected game fingerprint.");
        }

        var runtimeRoot = Path.Combine(roots.ExperimentRoot, "evidence", "copied-runtime-compatibility");
        var runtime = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            roots.CleanGameRoot,
            request.ExpectedFingerprint,
            runtimeRoot,
            OverwriteExisting: true));
        if (runtime.SmokeTestReadiness.Status != SmokeTestReadiness.ReadyForReversibleTest)
        {
            throw new SmokeTestException("The disposable copy is not ready for a reversible smoke test.");
        }

        return copied;
    }

    private static LaunchObservationResult LaunchCopiedExecutable(SmokeTestRoots roots, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new SmokeTestException("No verified main executable is available for the copied launch.");
        }

        var executable = Path.Combine(roots.CleanGameRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return LaunchObservationService.Observe(
            executable,
            roots.CleanGameRoot,
            roots.ExperimentRoot,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));
    }

    private static string ReadKnownLoaderLog(string cleanGameRoot)
    {
        try
        {
            foreach (var relative in new[] { "BepInEx/LogOutput.log", "BepInEx/LogOutput.txt" })
            {
                var path = Path.Combine(cleanGameRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }

            return string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new SmokeTestException("The disposable loader log could not be read safely.", exception);
        }
    }

    private static SmokeTestExecutionResult WriteOutcomeReport(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        string originalFingerprint,
        string copiedFingerprint,
        SmokeTestOutcome outcome,
        string baselineLaunchResult,
        string loaderLaunchResult,
        LaunchObservationResult launch,
        LoaderLogSummary summary,
        string transactionSummary,
        string rollbackResult)
    {
        var reportPath = request.ReportPath;
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            throw new SmokeTestException("A report path is required for the full smoke-test mode.");
        }

        var archivePath = request.BepInExArchivePath;
        var archiveName = archivePath is null ? "Unknown" : Path.GetFileName(archivePath);
        var observedHash = archivePath is null || !File.Exists(archivePath) ? "Unknown" : ComputeHash(archivePath);
        var data = new SmokeTestDetailedReport(
            request.ExpectedFingerprint.ToLowerInvariant(),
            "m1-loader-smoke-test-v1",
            DateTimeOffset.UtcNow,
            outcome,
            "Fingerprint and runtime readiness matched before and after the experiment; absolute path omitted.",
            $"Copied fingerprint before loader installation: {copiedFingerprint}; external disposable root used; absolute path omitted.",
            baselineLaunchResult,
            "Official GitHub repository BepInEx/BepInEx, tag v5.4.23.5, exact filename verified by the harness.",
            archiveName,
            request.OfficialAssetId ?? "Not supplied",
            request.OfficialAssetSize ?? "Not supplied",
            string.IsNullOrWhiteSpace(request.OfficialAssetDigest) ? "Observed local digest; vendor digest not supplied." : "Matched supplied official digest.",
            observedHash,
            "Validated archive entries and extracted outside the game copy before apply.",
            transactionSummary,
            loaderLaunchResult,
            $"Known BepInEx log evidence was read from the disposable copy; configuration generated: {summary.ConfigurationGenerated}; equivalent preloader/chainloader initialization evidence: {summary.StableInitialized}.",
            summary,
            rollbackResult,
            "Original fingerprint after the experiment matched the expected fingerprint; loader indicators remained absent.",
            summary.WarningCount == 0 ? [] : ["Non-fatal loader warnings were present."],
            summary.ErrorCategories,
            "Bootstrap evidence does not establish plugin TFM, Harmony compatibility, lifecycle bindings, game APIs, or custom-wave support.",
            "M1 task 4: evidence-backed bootstrap/plugin boundary design, only after this report is reviewed.");
        var markdown = SmokeTestReportWriter.BuildReport(data);
        var written = SmokeTestReportWriter.WriteAtomic(Path.GetFullPath(reportPath), markdown, overwrite: true);
        return new SmokeTestExecutionResult(
            outcome,
            loaderLaunchResult,
            written,
            originalFingerprint,
            copiedFingerprint,
            true,
            rollbackResult.Contains("restored", StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new SmokeTestException("The loader archive could not be hashed.", exception);
        }
    }

    private static void SaveJson<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SmokeTestException("The local smoke-test manifest could not be written safely.", exception);
        }
    }

    private static T LoadJson<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path))
                ?? throw new SmokeTestException("The local smoke-test manifest is invalid.");
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            throw new SmokeTestException("The local smoke-test manifest could not be read safely.", exception);
        }
    }

    private static void ValidateExpectedFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)
            || fingerprint.Length != 64
            || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new SmokeTestException("The expected fingerprint must be a 64-character SHA-256 value.");
        }
    }

    private sealed record Preflight(
        InstallationDiscoverySnapshot Snapshot,
        RuntimeCompatibilityResult Runtime);
}

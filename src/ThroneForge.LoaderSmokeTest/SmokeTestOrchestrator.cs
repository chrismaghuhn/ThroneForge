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

    public static SmokeTestExecutionResult Run(
        LoaderSmokeTestRequest request,
        SmokeTestExecutionHooks? hooks = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateExpectedFingerprint(request.ExpectedFingerprint);
        if (!string.IsNullOrWhiteSpace(request.ReportPath))
        {
            throw new SmokeTestException("Arbitrary report paths are not accepted; the committed report location is derived below the repository docs/discovery directory.");
        }

        hooks ??= new SmokeTestExecutionHooks();
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
            SmokeTestMode.Full => RunFull(request, roots, preflight, hooks, resume: false),
            SmokeTestMode.Resume => RunFull(request, roots, preflight, hooks, resume: true),
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
        DisposableProfileBaselineService.RequireFreshProfile(roots);
        var manifest = InstallationCopyService.Copy(roots);
        SaveBaseline(roots, request.ExpectedFingerprint, preflight.OriginalManifest, manifest);
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
            SaveBaseline(
                roots,
                request.ExpectedFingerprint,
                preflight.OriginalManifest,
                InstallationCopyService.CaptureManifest(roots.CleanGameRoot));
        }
        var plan = InstallArchive(request, roots);
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
        if (!File.Exists(baselinePath))
        {
            throw new SmokeTestException("No schema-backed disposable baseline exists for rollback.");
        }
        var baseline = LoadBaseline(roots);
        var manifestResult = InstallationCopyService.RestoreFilesToManifest(
            roots.CleanGameRoot,
            baseline.DisposableManifest);
        var copied = InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint;
        var succeeded = manifestResult.Matches
            && string.Equals(copied, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase);
        if (succeeded)
        {
            SmokeTestRecoveryMarkerService.Clear(Path.Combine(roots.ManifestsRoot, "recovery-marker.json"));
        }
        return new SmokeTestExecutionResult(
            succeeded ? SmokeTestOutcome.Passed : SmokeTestOutcome.Failed,
            succeeded ? "Disposable profile rollback restored the expected complete manifest and fingerprint." : "Disposable profile rollback did not restore the expected complete manifest and fingerprint.",
            null,
            preflight.Snapshot.Fingerprint,
            copied,
            true,
            succeeded);
    }

    private static SmokeTestExecutionResult RunFull(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight,
        SmokeTestExecutionHooks hooks,
        bool resume)
    {
        CopyManifest copyManifest;
        if (resume)
        {
            var baseline = LoadBaseline(roots);
            copyManifest = baseline.DisposableManifest;
            var current = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
            DisposableProfileBaselineService.LoadAndValidateResume(
                BaselinePath(roots),
                request.ExpectedFingerprint,
                preflight.OriginalManifest,
                current,
                preflight.Runtime.SmokeTestReadiness.Status,
                preflight.Runtime.LoaderIndicators);
        }
        else
        {
            DisposableProfileBaselineService.RequireFreshProfile(roots);
            copyManifest = InstallationCopyService.Copy(roots);
            SaveBaseline(roots, request.ExpectedFingerprint, preflight.OriginalManifest, copyManifest);
        }

        var copiedSnapshot = EnsureCopiedProfile(roots, request, preflight, copyManifest);
        var baselineLaunch = hooks.Launch is null
            ? LaunchCopiedExecutable(roots, copiedSnapshot.SelectedExecutableRelativePath)
            : hooks.Launch(roots, copiedSnapshot.SelectedExecutableRelativePath!);
        if (!baselineLaunch.Started || !baselineLaunch.StableInitialized || baselineLaunch.RequiresManualClosure)
        {
            return WriteOutcomeReport(
                request,
                roots,
                preflight.Snapshot.Fingerprint,
                copiedSnapshot.Fingerprint,
                SmokeTestOutcome.Inconclusive,
                "BaselineLaunchInconclusive: the copied executable did not reach a bounded stable state or requires manual closure.",
                "Not attempted because the baseline launch was inconclusive.",
                baselineLaunch,
                new LoaderLogSummary(null, false, false, false, 0, 0, 0, 0, [], false),
                "No loader transaction was attempted.",
                "No loader was installed.",
                preflight,
                copyManifest,
                null,
                hooks,
                SmokeTestRollbackState.NotApplied);
        }

        var plan = InstallArchive(request, roots);
        var guard = SmokeTestPostApplyGuard.Execute(
            launch: () => hooks.Launch is null
                ? LaunchCopiedExecutable(roots, copiedSnapshot.SelectedExecutableRelativePath)
                : hooks.Launch(roots, copiedSnapshot.SelectedExecutableRelativePath!),
            readLog: () => hooks.ReadLoaderLog is null ? ReadKnownLoaderLog(roots.CleanGameRoot) : hooks.ReadLoaderLog(roots.CleanGameRoot),
            parseLog: text => hooks.ParseLoaderLog is null ? LoaderLogParser.Parse(text) : hooks.ParseLoaderLog(text),
            classify: summary => SmokeTestOutcomeClassifier.Classify(true, true, summary),
            rollback: () => RollbackAppliedProfile(roots, plan, copyManifest),
            writeRecoveryMarker: () => SaveRecoveryMarker(roots));

        var loaderLaunch = guard.Launch ?? new LaunchObservationResult(false, false, true, null, true, false, TimeSpan.Zero, guard.FailureCategory);
        var summary = guard.LogSummary ?? new LoaderLogSummary(null, false, false, false, 0, 0, 0, 0, [], false);
        if (guard.RollbackState == SmokeTestRollbackState.ManualClosureRequired)
        {
            return WriteOutcomeReport(
                request,
                roots,
                preflight.Snapshot.Fingerprint,
                copiedSnapshot.Fingerprint,
                SmokeTestOutcome.Inconclusive,
                "The copied loader process requires manual graceful closure.",
                "Manual closure is required before rollback can proceed.",
                loaderLaunch,
                summary,
                "Validated archive extraction and transactional apply completed.",
                "Rollback deferred while the copied process remains active.",
                preflight,
                copyManifest,
                null,
                hooks,
                guard.RollbackState);
        }

        var copiedAfterRollback = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var originalPost = VerifyOriginalAfterExperiment(request, roots, preflight);
        var rollbackVerified = guard.RollbackState == SmokeTestRollbackState.RollbackSucceeded
            && InstallationCopyService.CompareManifests(copyManifest, copiedAfterRollback).Matches
            && string.Equals(InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase);
        var outcome = guard.Outcome;
        if (guard.Launch is not null && (!guard.Launch.Started || !guard.Launch.StableInitialized))
        {
            outcome = SmokeTestOutcome.Failed;
        }
        if (!rollbackVerified || !originalPost.Passed)
        {
            outcome = SmokeTestOutcome.Failed;
        }

        return WriteOutcomeReport(
            request,
            roots,
            originalPost.Snapshot.Fingerprint,
            InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint,
            outcome,
            "The copied executable reached a bounded stable state and was gracefully closed before installation.",
            guard.RollbackState == SmokeTestRollbackState.ManualClosureRequired
                ? "Manual closure is required; rollback was intentionally deferred."
                : "The loader-enabled copied executable completed bounded observation.",
            loaderLaunch,
            summary,
            "Validated archive extraction and transactional apply completed.",
            rollbackVerified ? "Rollback restored the complete copied manifest and fingerprint." : "Rollback did not restore the complete copied manifest and fingerprint.",
            preflight,
            copyManifest,
            new PostExperimentVerification(originalPost, rollbackVerified, guard.RollbackState),
            hooks,
            guard.RollbackState);
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
        SaveJson(Path.Combine(roots.ManifestsRoot, "transaction-plan.json"), plan);
        LoaderTransactionService.Apply(roots, plan, extracted);
        return plan;
    }

    private static bool RollbackAppliedProfile(
        SmokeTestRoots roots,
        TransactionPlan plan,
        CopyManifest baseline)
    {
        try
        {
            LoaderTransactionService.Rollback(roots, plan);
            return InstallationCopyService.RestoreFilesToManifest(roots.CleanGameRoot, baseline).Matches;
        }
        catch (SmokeTestException)
        {
            return false;
        }
    }

    private static void SaveRecoveryMarker(SmokeTestRoots roots)
    {
        SmokeTestRecoveryMarkerService.Write(Path.Combine(roots.ManifestsRoot, "recovery-marker.json"));
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

        return new Preflight(snapshot, runtime, InstallationCopyService.CaptureManifest(roots.OriginalGameRoot));
    }

    private static OriginalPostVerification VerifyOriginalAfterExperiment(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        var snapshot = InstallationFingerprintService.Capture(roots.OriginalGameRoot);
        var runtimeRoot = Path.Combine(roots.ExperimentRoot, "evidence", "original-post-runtime-compatibility");
        var runtime = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            roots.OriginalGameRoot,
            request.ExpectedFingerprint,
            runtimeRoot,
            OverwriteExisting: true));
        var manifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
        var manifestResult = InstallationCopyService.CompareManifests(preflight.OriginalManifest, manifest);
        var fingerprintMatches = string.Equals(snapshot.Fingerprint, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase);
        var readinessMatches = runtime.SmokeTestReadiness.Status == SmokeTestReadiness.ReadyForReversibleTest;
        var indicatorsAbsent = runtime.LoaderIndicators.All(item => item.Status == LoaderIndicatorStatus.Absent);
        var backendMatches = runtime.ManagedRuntimeProfile == ManagedRuntimeProfile.Mono;
        var architectureMatches = runtime.ExecutableArchitecture == ExecutableArchitecture.X64;
        var unityMatches = string.Equals(runtime.UnityVersion, "2022.3.62f2", StringComparison.OrdinalIgnoreCase);
        var targetFrameworkMatches = runtime.TargetFrameworkRecommendation == TargetFrameworkRecommendation.Netstandard21Candidate;
        var confidenceMatches = runtime.TargetFrameworkAssessment.Confidence == TargetFrameworkConfidence.Medium;
        return new OriginalPostVerification(
            snapshot,
            runtime,
            manifestResult,
            fingerprintMatches,
            readinessMatches,
            indicatorsAbsent,
            backendMatches,
            architectureMatches,
            unityMatches,
            targetFrameworkMatches,
            confidenceMatches);
    }

    private static string BaselinePath(SmokeTestRoots roots)
        => Path.Combine(roots.ManifestsRoot, "baseline-copy-manifest.json");

    private static void SaveBaseline(
        SmokeTestRoots roots,
        string expectedFingerprint,
        CopyManifest originalManifest,
        CopyManifest disposableManifest)
        => DisposableProfileBaselineService.Save(
            BaselinePath(roots),
            new DisposableProfileBaseline(
                DisposableProfileBaselineService.SchemaVersion,
                DisposableProfileBaselineService.TaskVersion,
                expectedFingerprint.ToLowerInvariant(),
                originalManifest,
                disposableManifest));

    private static DisposableProfileBaseline LoadBaseline(SmokeTestRoots roots)
        => LoadJson<DisposableProfileBaseline>(BaselinePath(roots));

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

        if (manifest is not null
            && !InstallationCopyService.CompareManifests(manifest, InstallationCopyService.CaptureManifest(roots.CleanGameRoot)).Matches)
        {
            throw new SmokeTestException("The disposable copy does not match the saved complete baseline manifest.");
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
        string rollbackResult,
        Preflight preflight,
        CopyManifest copyManifest,
        PostExperimentVerification? postVerification,
        SmokeTestExecutionHooks hooks,
        SmokeTestRollbackState rollbackState)
    {
        var reportPath = SmokeTestPathValidator.ValidateCommittedReportPath(roots, request.ExpectedFingerprint);

        var archivePath = request.BepInExArchivePath;
        var archiveName = archivePath is null ? "Unknown" : Path.GetFileName(archivePath);
        var observedHash = archivePath is null || !File.Exists(archivePath) ? "Unknown" : ComputeHash(archivePath);
        var originalManifestText = postVerification is null
            ? "Historical Task-3 evidence did not retain a complete original manifest for this state."
            : postVerification.Original.ManifestComparison.Matches
                ? "Matches the complete original pre-experiment manifest (relative paths, sizes, and SHA-256 values)."
                : "Does not match the complete original pre-experiment manifest.";
        var originalRuntimeText = postVerification is null
            ? "Post-experiment runtime/readiness inspection was not performed while manual closure was required."
            : $"Readiness={postVerification.Original.Runtime.SmokeTestReadiness.Status}; backend={postVerification.Original.Runtime.ManagedRuntimeProfile}; architecture={postVerification.Original.Runtime.ExecutableArchitecture}; Unity={postVerification.Original.Runtime.UnityVersion}; TFM={postVerification.Original.Runtime.TargetFrameworkRecommendation}; confidence={postVerification.Original.Runtime.TargetFrameworkAssessment.Confidence}.";
        var indicatorText = postVerification is null
            ? "Post-experiment loader-indicator inspection was deferred because the copied process remained active."
            : postVerification.Original.IndicatorsAbsent
                ? "All inspected original-installation loader indicators were Absent."
                : "One or more original-installation loader indicators were non-absent.";
        var disposableText = postVerification is null
            ? "Complete disposable-manifest rollback comparison was deferred because manual closure is required."
            : postVerification.DisposableManifestMatches
                ? "Matches the complete disposable pre-installation manifest (relative paths, sizes, directories, and SHA-256 values), plus fingerprint v1."
                : "Does not match the complete disposable pre-installation manifest.";
        var data = new SmokeTestDetailedReport(
            request.ExpectedFingerprint.ToLowerInvariant(),
            DisposableProfileBaselineService.TaskVersion,
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
            postVerification is null
                ? "Post-experiment original verification was deferred; compatibility fingerprint was unchanged before manual closure."
                : postVerification.Original.Passed
                    ? "Complete original manifest, compatibility fingerprint, runtime readiness, and expected compatibility evidence matched."
                    : "One or more original post-verification checks failed.",
            summary.WarningCount == 0 ? [] : ["Non-fatal loader warnings were present."],
            summary.ErrorCategories,
            "Bootstrap evidence does not establish plugin TFM, Harmony compatibility, lifecycle bindings, game APIs, or custom-wave support.",
            "M1 task 4: evidence-backed bootstrap/plugin boundary design, only after this report is reviewed.",
            originalManifestText,
            originalRuntimeText,
            indicatorText,
            disposableText,
            rollbackState == SmokeTestRollbackState.ManualClosureRequired
                ? "ManualClosureRequired. After graceful closure, run `dotnet exec <loader-smoke-test.dll> Rollback --game-path <redacted> --experiment-root <redacted> --expected-fingerprint <fingerprint> --repository-root <redacted>`; no automatic cleanup was attempted."
                : rollbackState.ToString());
        string written;
        try
        {
            var markdown = hooks.BuildReport is null ? SmokeTestReportWriter.BuildReport(data) : hooks.BuildReport(data);
            written = hooks.WriteReport is null
                ? SmokeTestReportWriter.WriteAtomic(reportPath, markdown, overwrite: true)
                : hooks.WriteReport(reportPath, markdown, true);
        }
        catch (SmokeTestException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SmokeTestException("The sanitized smoke-test report could not be prepared or written safely.", exception);
        }
        return new SmokeTestExecutionResult(
            outcome,
            loaderLaunchResult,
            written,
            originalFingerprint,
            copiedFingerprint,
            postVerification?.Original.Passed ?? false,
            postVerification?.DisposableManifestMatches == true
                && rollbackState == SmokeTestRollbackState.RollbackSucceeded);
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
        RuntimeCompatibilityResult Runtime,
        CopyManifest OriginalManifest);

    private sealed record OriginalPostVerification(
        InstallationDiscoverySnapshot Snapshot,
        RuntimeCompatibilityResult Runtime,
        ManifestVerificationResult ManifestComparison,
        bool FingerprintMatches,
        bool ReadinessMatches,
        bool IndicatorsAbsent,
        bool BackendMatches,
        bool ArchitectureMatches,
        bool UnityMatches,
        bool TargetFrameworkMatches,
        bool ConfidenceMatches)
    {
        public bool Passed => ManifestComparison.Matches
            && FingerprintMatches
            && ReadinessMatches
            && IndicatorsAbsent
            && BackendMatches
            && ArchitectureMatches
            && UnityMatches
            && TargetFrameworkMatches
            && ConfidenceMatches;
    }

    private sealed record PostExperimentVerification(
        OriginalPostVerification Original,
        bool DisposableManifestMatches,
        SmokeTestRollbackState RollbackState);
}

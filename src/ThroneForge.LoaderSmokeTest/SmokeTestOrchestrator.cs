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
    bool RollbackVerified,
    bool RecoveryMarkerPersisted = false,
    string? RecoveryMarkerFailureCategory = null);

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
        var baselineManifest = RequireCleanStagedBaseline(request, roots, preflight);
        EnsureCopiedProfile(roots, request, preflight, baselineManifest.DisposableManifest);
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
        var baseline = RequireCleanStagedBaseline(request, roots, preflight);
        EnsureCopiedProfile(roots, request, preflight, baseline.DisposableManifest);
        _ = InstallArchive(request, roots, baseline.DisposableManifest);
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
        var state = RequireInstalledStagedProfile(
            request,
            roots,
            preflight,
            [LoaderTransactionStatus.Applied]);
        LaunchObservationResult? launch = null;
        LoaderLogSummary? summary = null;
        try
        {
            launch = LaunchCopiedExecutable(
                roots,
                preflight.Snapshot.SelectedExecutableRelativePath,
                TimeSpan.FromSeconds(60));
            summary = LoaderLogParser.Parse(ReadKnownLoaderLog(roots.CleanGameRoot));
            var bootstrapObserved = LoaderBootstrapLaunchCriteria.IsObserved(launch, summary);
            var status = bootstrapObserved
                ? LoaderTransactionStatus.LaunchObserved
                : LoaderTransactionStatus.RollbackRequired;
            var generatedFiles = LoaderTransactionStateService.CaptureGeneratedEvidence(
                state.ExpectedAppliedManifest,
                InstallationCopyService.CaptureManifest(roots.CleanGameRoot),
                out var generatedDirectories);

            LoaderTransactionStateService.SaveAtomic(
                LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                state with
                {
                    Status = status,
                    GeneratedEvidenceFiles = generatedFiles,
                    GeneratedEvidenceDirectories = generatedDirectories,
                    LaunchEvidence = ToBootstrapEvidence(summary)
                });

            return new SmokeTestExecutionResult(
                SmokeTestOutcomeClassifier.Classify(true, bootstrapObserved, summary),
                launch.FailureCategory,
                null,
                preflight.Snapshot.Fingerprint,
                InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint,
                true,
                false);
        }
        catch (Exception exception)
        {
            try
            {
                LoaderTransactionStateService.SaveAtomic(
                    LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                    state with
                    {
                        Status = LoaderTransactionStatus.RollbackRequired,
                        GeneratedEvidenceFiles = [],
                        GeneratedEvidenceDirectories = [],
                        LaunchEvidence = summary is null ? null : ToBootstrapEvidence(summary)
                    });
            }
            catch (Exception stateException)
            {
                throw new SmokeTestException(
                    "The staged loader launch failed and its transaction could not be marked for rollback.",
                    stateException);
            }

            throw exception is SmokeTestException smokeTestException
                ? new SmokeTestException("The staged loader launch failed; rollback is required.", smokeTestException)
                : new SmokeTestException("The staged loader launch failed; rollback is required.", exception);
        }
    }

    private static SmokeTestExecutionResult Verify(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        var state = RequireInstalledStagedProfile(
            request,
            roots,
            preflight,
            [LoaderTransactionStatus.LaunchObserved]);
        LoaderTransactionStateService.VerifyAppliedProfile(roots, state);
        var copied = InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint;
        var original = InstallationFingerprintService.Capture(roots.OriginalGameRoot).Fingerprint;
        if (!string.Equals(original, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new SmokeTestException("The original installation changed during the experiment.");
        }

        var loaderVerified = state.LaunchEvidence?.MeetsBootstrapCriteria == true;

        return new SmokeTestExecutionResult(
            copied == request.ExpectedFingerprint && loaderVerified ? SmokeTestOutcome.Passed : SmokeTestOutcome.Failed,
            copied == request.ExpectedFingerprint && loaderVerified
                ? "Original preflight, applied transaction state, loader evidence, and copied profile verification succeeded."
                : "The staged loader transaction or required BepInEx bootstrap evidence could not be verified.",
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
        var state = RequireInstalledStagedProfile(
            request,
            roots,
            preflight,
            [
                LoaderTransactionStatus.Applied,
                LoaderTransactionStatus.LaunchObserved,
                LoaderTransactionStatus.RollbackRequired
            ],
            verifyApplied: false);
        var currentManifest = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var generatedFiles = LoaderTransactionStateService.CaptureRollbackGeneratedEvidence(
            state.ExpectedAppliedManifest,
            currentManifest,
            out var generatedDirectories);
        state = state with
        {
            GeneratedEvidenceFiles = generatedFiles,
            GeneratedEvidenceDirectories = generatedDirectories
        };
        LoaderTransactionStateService.SaveAtomic(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots), state);
        var plan = new TransactionPlan(roots.ExtractedLoaderRoot, state.Entries);
        LoaderTransactionService.Rollback(roots, plan);
        var baseline = LoadBaseline(roots);
        var manifestResult = InstallationCopyService.RestoreFilesToManifest(
            roots.CleanGameRoot,
            baseline.DisposableManifest);
        var copied = InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint;
        var succeeded = manifestResult.Matches
            && string.Equals(copied, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase)
            && VerifyCopiedReadinessAfterRollback(request, roots);
        if (succeeded)
        {
            SmokeTestRecoveryMarkerService.Clear(Path.Combine(roots.ManifestsRoot, "recovery-marker.json"));
            LoaderTransactionStateService.SaveAtomic(
                LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                state with
                {
                    Status = LoaderTransactionStatus.RolledBack,
                    GeneratedEvidenceFiles = [],
                    GeneratedEvidenceDirectories = [],
                    LaunchEvidence = null
                });
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
                LoaderSmokeTestStatePaths.GetBaselinePath(roots),
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

        var transaction = InstallArchive(request, roots, copyManifest);
        var guard = SmokeTestPostApplyGuard.Execute(
            launch: () => hooks.Launch is null
                ? LaunchCopiedExecutable(roots, copiedSnapshot.SelectedExecutableRelativePath)
                : hooks.Launch(roots, copiedSnapshot.SelectedExecutableRelativePath!),
            readLog: () => hooks.ReadLoaderLog is null ? ReadKnownLoaderLog(roots.CleanGameRoot) : hooks.ReadLoaderLog(roots.CleanGameRoot),
            parseLog: text => hooks.ParseLoaderLog is null ? LoaderLogParser.Parse(text) : hooks.ParseLoaderLog(text),
            classify: summary => SmokeTestOutcomeClassifier.Classify(true, true, summary),
            rollback: () => RollbackAppliedProfile(roots, transaction.State, copyManifest),
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
                guard.RollbackState,
                guard.RecoveryMarkerPersisted,
                guard.RecoveryMarkerFailureCategory);
        }

        var copiedAfterRollback = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var originalPost = VerifyOriginalAfterExperiment(request, roots, preflight);
        var rollbackVerified = guard.RollbackState == SmokeTestRollbackState.RollbackSucceeded
            && InstallationCopyService.CompareManifests(copyManifest, copiedAfterRollback).Matches
            && string.Equals(InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint, request.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase);
        var outcome = guard.Outcome;
        if (guard.Launch is not null && !LoaderBootstrapLaunchCriteria.IsObserved(guard.Launch, summary))
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

    private static (TransactionPlan Plan, LoaderTransactionState State) InstallArchive(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        CopyManifest baselineManifest)
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

        EnsureTransactionCanBeReplaced(request, roots, baselineManifest);
        var extracted = ArchiveSafetyService.Extract(archivePath, roots.ExtractedLoaderRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);
        var prepared = new LoaderTransactionState(
            LoaderTransactionStateService.SchemaVersion,
            LoaderTransactionStateService.TaskVersion,
            request.ExpectedFingerprint.ToLowerInvariant(),
            InstallationCopyService.ComputeManifestIdentity(baselineManifest),
            ExpectedArchiveName,
            observedHash,
            LoaderTransactionStatus.Prepared,
            LoaderTransactionService.BuildExpectedAppliedManifest(baselineManifest, extracted),
            plan.Entries,
            []);
        LoaderTransactionStateService.SaveAtomic(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots), prepared);
        try
        {
            LoaderTransactionService.Apply(roots, plan, extracted);
            LoaderTransactionStateService.VerifyAppliedProfile(roots, prepared);
            var applied = prepared with { Status = LoaderTransactionStatus.Applied };
            LoaderTransactionStateService.SaveAtomic(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots), applied);
            return (plan, applied);
        }
        catch (Exception exception)
        {
            var rollbackSucceeded = false;
            try
            {
                LoaderTransactionService.Rollback(roots, plan);
                rollbackSucceeded = true;
            }
            catch (SmokeTestException)
            {
            }

            if (rollbackSucceeded)
            {
                LoaderTransactionStateService.SaveAtomic(
                    LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                    prepared with { Status = LoaderTransactionStatus.FailedAndRolledBack });
            }

            if (exception is SmokeTestException smokeTestException)
            {
                throw new SmokeTestException(
                    rollbackSucceeded
                        ? "The loader transaction failed and was rolled back; the persisted state is not launchable."
                        : "The loader transaction failed and rollback could not be verified.",
                    smokeTestException);
            }

            throw new SmokeTestException(
                rollbackSucceeded
                    ? "The loader transaction failed and was rolled back; the persisted state is not launchable."
                    : "The loader transaction failed and rollback could not be verified.",
                exception);
        }
    }

    private static bool RollbackAppliedProfile(
        SmokeTestRoots roots,
        LoaderTransactionState state,
        CopyManifest baseline)
    {
        try
        {
            LoaderTransactionService.ValidatePersistedEntries(roots, state.Entries);
            var plan = new TransactionPlan(roots.ExtractedLoaderRoot, state.Entries);
            LoaderTransactionService.Rollback(roots, plan);
            var restored = InstallationCopyService.RestoreFilesToManifest(roots.CleanGameRoot, baseline).Matches;
            if (restored)
            {
                LoaderTransactionStateService.SaveAtomic(
                    LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                    state with
                    {
                        Status = LoaderTransactionStatus.RolledBack,
                        GeneratedEvidenceFiles = [],
                        GeneratedEvidenceDirectories = [],
                        LaunchEvidence = null
                    });
            }

            return restored;
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

    private static void SaveBaseline(
        SmokeTestRoots roots,
        string expectedFingerprint,
        CopyManifest originalManifest,
        CopyManifest disposableManifest)
        => DisposableProfileBaselineService.Save(
            LoaderSmokeTestStatePaths.GetBaselinePath(roots),
            new DisposableProfileBaseline(
                DisposableProfileBaselineService.SchemaVersion,
                DisposableProfileBaselineService.TaskVersion,
                expectedFingerprint.ToLowerInvariant(),
                originalManifest,
                disposableManifest));

    private static DisposableProfileBaseline LoadBaseline(SmokeTestRoots roots)
        => LoadJson<DisposableProfileBaseline>(LoaderSmokeTestStatePaths.GetBaselinePath(roots));

    private static void EnsureTransactionCanBeReplaced(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        CopyManifest baselineManifest)
    {
        var legacyPlanPath = Path.Combine(roots.ManifestsRoot, "transaction-plan.json");
        if (File.Exists(legacyPlanPath))
        {
            throw new SmokeTestException("An unversioned loader transaction plan exists; it must be explicitly removed or rolled back before installation.");
        }

        if (!File.Exists(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots)))
        {
            return;
        }

        var existing = LoaderTransactionStateService.LoadAndValidate(
            LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
            roots,
            request.ExpectedFingerprint,
            baselineManifest,
            Enum.GetValues<LoaderTransactionStatus>());
        if (existing.Status is not (LoaderTransactionStatus.RolledBack or LoaderTransactionStatus.FailedAndRolledBack))
        {
            throw new SmokeTestException("An active or stale loader transaction exists; run Rollback successfully before installing again.");
        }
    }

    private static bool VerifyCopiedReadinessAfterRollback(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots)
    {
        var runtimeRoot = Path.Combine(roots.ExperimentRoot, "evidence", "copied-post-rollback-runtime");
        var runtime = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            roots.CleanGameRoot,
            request.ExpectedFingerprint,
            runtimeRoot,
            OverwriteExisting: true));
        return runtime.SmokeTestReadiness.Status == SmokeTestReadiness.ReadyForReversibleTest
            && runtime.LoaderIndicators.All(item => item.Status == LoaderIndicatorStatus.Absent);
    }

    private static LoaderBootstrapEvidence ToBootstrapEvidence(LoaderLogSummary summary)
        => new(
            summary.BepInExVersion,
            summary.PreloaderInitialized,
            summary.ChainloaderInitialized,
            summary.PluginsDiscovered,
            summary.WarningCount,
            summary.ErrorCount,
            summary.FatalErrorCount);

    private static DisposableProfileBaseline RequireCleanStagedBaseline(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight)
    {
        if (!File.Exists(LoaderSmokeTestStatePaths.GetBaselinePath(roots)))
        {
            throw new SmokeTestException("No schema-backed disposable baseline exists; run Prepare or a fresh Full mode first.");
        }

        if (!Directory.Exists(roots.CleanGameRoot))
        {
            throw new SmokeTestException("The disposable copy does not exist; run Prepare first.");
        }

        var currentManifest = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var runtimeRoot = Path.Combine(roots.ExperimentRoot, "evidence", "staged-runtime-compatibility");
        var runtime = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            roots.CleanGameRoot,
            request.ExpectedFingerprint,
            runtimeRoot,
            OverwriteExisting: true));
        return DisposableProfileBaselineService.LoadAndValidateResume(
            LoaderSmokeTestStatePaths.GetBaselinePath(roots),
            request.ExpectedFingerprint,
            preflight.OriginalManifest,
            currentManifest,
            runtime.SmokeTestReadiness.Status,
            runtime.LoaderIndicators);
    }

    private static LoaderTransactionState RequireInstalledStagedProfile(
        LoaderSmokeTestRequest request,
        SmokeTestRoots roots,
        Preflight preflight,
        IEnumerable<LoaderTransactionStatus> allowedStatuses,
        bool verifyApplied = true)
    {
        var baselinePath = LoaderSmokeTestStatePaths.GetBaselinePath(roots);
        if (!File.Exists(baselinePath))
        {
            throw new SmokeTestException("No schema-backed disposable baseline exists; run Prepare or a fresh Full mode first.");
        }

        if (!Directory.Exists(roots.CleanGameRoot))
        {
            throw new SmokeTestException("The disposable copy does not exist; run Prepare first.");
        }

        var baseline = DisposableProfileBaselineService.LoadAndValidateSavedBaseline(
            baselinePath,
            request.ExpectedFingerprint,
            preflight.OriginalManifest);

        if (!File.Exists(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots)))
        {
            throw new SmokeTestException("No verified loader transaction exists for this staged mode.");
        }

        var state = LoaderTransactionStateService.LoadAndValidate(
            LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
            roots,
            request.ExpectedFingerprint,
            baseline.DisposableManifest,
            allowedStatuses);
        if (verifyApplied && state.Status is (LoaderTransactionStatus.Applied or LoaderTransactionStatus.LaunchObserved))
        {
            LoaderTransactionStateService.VerifyAppliedProfile(roots, state);
        }

        return state;
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

    private static LaunchObservationResult LaunchCopiedExecutable(
        SmokeTestRoots roots,
        string? relativePath,
        TimeSpan? observationWindow = null)
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
            observationWindow ?? TimeSpan.FromSeconds(10),
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
        SmokeTestRollbackState rollbackState,
        bool recoveryMarkerPersisted = false,
        string? recoveryMarkerFailureCategory = null)
    {
        var reportPath = SmokeTestPathValidator.ValidateCommittedReportPath(roots, request.ExpectedFingerprint);

        var archivePath = request.BepInExArchivePath;
        var archiveName = archivePath is null ? "Unknown" : Path.GetFileName(archivePath);
        var observedHash = archivePath is null || !File.Exists(archivePath) ? "Unknown" : ComputeHash(archivePath);
        var manualClosureDeferred = rollbackState == SmokeTestRollbackState.ManualClosureRequired;
        var originalManifestText = postVerification is null
            ? manualClosureDeferred
                ? "Complete original-manifest comparison is deferred until the copied process is closed and rollback completes."
                : "Complete original-manifest comparison was not required because no loader transaction was applied."
            : postVerification.Original.ManifestComparison.Matches
                ? "Matches the complete original pre-experiment manifest (relative paths, sizes, and SHA-256 values)."
                : "Does not match the complete original pre-experiment manifest.";
        var originalRuntimeText = postVerification is null
            ? manualClosureDeferred
                ? "Complete original runtime/readiness inspection is deferred until the copied process is closed and rollback completes."
                : "Complete original runtime/readiness inspection was not required because no loader transaction was applied."
            : $"Readiness={postVerification.Original.Runtime.SmokeTestReadiness.Status}; backend={postVerification.Original.Runtime.ManagedRuntimeProfile}; architecture={postVerification.Original.Runtime.ExecutableArchitecture}; Unity={postVerification.Original.Runtime.UnityVersion}; TFM={postVerification.Original.Runtime.TargetFrameworkRecommendation}; confidence={postVerification.Original.Runtime.TargetFrameworkAssessment.Confidence}.";
        var indicatorText = postVerification is null
            ? manualClosureDeferred
                ? "Original-installation loader-indicator inspection is deferred until the copied process is closed and rollback completes."
                : "Original-installation loader-indicator inspection was not required because no loader transaction was applied."
            : postVerification.Original.IndicatorsAbsent
                ? "All inspected original-installation loader indicators were Absent."
                : "One or more original-installation loader indicators were non-absent.";
        var disposableText = postVerification is null
            ? manualClosureDeferred
                ? "Complete disposable-manifest rollback comparison is deferred until the copied process is closed."
                : "Complete disposable-manifest rollback comparison was not required because no loader transaction was applied."
            : postVerification.DisposableManifestMatches
                ? "Matches the complete disposable pre-installation manifest (relative paths, sizes, directories, and SHA-256 values), plus fingerprint v1."
                : "Does not match the complete disposable pre-installation manifest.";
        var originalVerificationEvidence = postVerification is null
            ? rollbackState == SmokeTestRollbackState.ManualClosureRequired
                ? new OriginalInstallationVerificationEvidence(
                    OriginalInstallationVerificationState.ManualClosureDeferred,
                    [])
                : new OriginalInstallationVerificationEvidence(
                    OriginalInstallationVerificationState.NoTransactionApplied,
                    [])
            : postVerification.Original.Passed
                ? new OriginalInstallationVerificationEvidence(
                    OriginalInstallationVerificationState.CompletePostCheckPassed,
                    [])
                : new OriginalInstallationVerificationEvidence(
                    OriginalInstallationVerificationState.CompletePostCheckFailed,
                    postVerification.Original.FailedCategories);
        var recoveryState = rollbackState == SmokeTestRollbackState.ManualClosureRequired
            ? recoveryMarkerPersisted
                ? "ManualClosureRequired — recovery marker persisted. After graceful closure, run `dotnet exec <loader-smoke-test.dll> Rollback --game-path <redacted> --experiment-root <redacted> --expected-fingerprint <fingerprint> --repository-root <redacted>`; no automatic cleanup was attempted."
                : $"ManualClosureRequired — recovery marker unavailable ({recoveryMarkerFailureCategory ?? "marker-unavailable"}). After graceful closure, run `dotnet exec <loader-smoke-test.dll> Rollback --game-path <redacted> --experiment-root <redacted> --expected-fingerprint <fingerprint> --repository-root <redacted>`; no automatic cleanup was attempted."
            : rollbackState.ToString();
        var resultMessage = loaderLaunchResult;
        if (rollbackState == SmokeTestRollbackState.ManualClosureRequired)
        {
            resultMessage += recoveryMarkerPersisted
                ? " Recovery marker persisted."
                : $" Recovery marker unavailable ({recoveryMarkerFailureCategory ?? "marker-unavailable"}); use the explicit redacted Rollback command after graceful closure.";
        }
        var data = new SmokeTestDetailedReport(
            request.ExpectedFingerprint.ToLowerInvariant(),
            DisposableProfileBaselineService.TaskVersion,
            DateTimeOffset.UtcNow,
            outcome,
            originalVerificationEvidence.ToReportText(),
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
            resultMessage,
            $"Known BepInEx log evidence was read from the disposable copy; configuration generated: {summary.ConfigurationGenerated}; equivalent preloader/chainloader initialization evidence: {summary.StableInitialized}.",
            summary,
            rollbackResult,
            postVerification is null
                ? manualClosureDeferred
                    ? "Preflight passed; complete post-verification is deferred until the copied process is closed and rollback completes."
                    : "Preflight passed; complete post-verification was not required because no loader transaction was applied."
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
            recoveryState);
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
            resultMessage,
            written,
            originalFingerprint,
            copiedFingerprint,
            postVerification?.Original.Passed ?? false,
            postVerification?.DisposableManifestMatches == true
                && rollbackState == SmokeTestRollbackState.RollbackSucceeded,
            recoveryMarkerPersisted,
            recoveryMarkerFailureCategory);
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

        public IReadOnlyList<string> FailedCategories
        {
            get
            {
                var categories = new List<string>();
                if (!ManifestComparison.Matches) categories.Add("complete manifest");
                if (!FingerprintMatches) categories.Add("fingerprint");
                if (!ReadinessMatches) categories.Add("readiness");
                if (!IndicatorsAbsent) categories.Add("loader indicators");
                if (!BackendMatches) categories.Add("backend");
                if (!ArchitectureMatches) categories.Add("architecture");
                if (!UnityMatches) categories.Add("Unity version");
                if (!TargetFrameworkMatches) categories.Add("TFM recommendation");
                if (!ConfidenceMatches) categories.Add("TFM confidence");
                return categories;
            }
        }
    }

    private sealed record PostExperimentVerification(
        OriginalPostVerification Original,
        bool DisposableManifestMatches,
        SmokeTestRollbackState RollbackState);
}

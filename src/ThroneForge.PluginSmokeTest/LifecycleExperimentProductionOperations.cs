using System.Security.Cryptography;
using ThroneForge.Contracts;
using ThroneForge.Discovery;
using ThroneForge.LoaderSmokeTest;
using ThroneForge.Runtime;

namespace ThroneForge.PluginSmokeTest;

public sealed record LifecycleExperimentProductionOptions(
    string RepositoryRoot,
    string OriginalGameRoot,
    string ExperimentRoot,
    string ExpectedFingerprint,
    string BepInExArchivePath,
    string ExpectedBepInExDigest,
    string PackageRoot,
    string ManifestPath,
    string UnityAssemblyPath,
    string ExecutableRelativePath,
    string Nonce,
    string AdapterId = "throneforge.adapter",
    string AdapterVersion = "1.0.0");

/// <summary>
/// Production adapter for the real Task-7 CLI. It calls the existing Task-3/Task-6 services and
/// returns typed evidence to the orchestrator; it does not interpret stages or write Markdown.
/// </summary>
public sealed class LifecycleExperimentProductionOperations : ILifecycleExperimentOperations
{
    private readonly LifecycleExperimentProductionOptions options;
    private readonly SmokeTestRoots roots;
    private CopyManifest? originalManifest;
    private CopyManifest? loaderOnlyManifest;
    private DisposableProfileBaseline? baseline;
    private CapturedPluginPackage? capturedPackage;
    private string? lifecycleLog;
    private string? packageDigest;
    private string? admissionBindingDigest;
    private string? selectedExecutableRelativePath;

    public LifecycleExperimentProductionOperations(LifecycleExperimentProductionOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        roots = SmokeTestPathValidator.ValidateRoots(options.RepositoryRoot, options.OriginalGameRoot, options.ExperimentRoot);
    }

    public OriginalPreflightEvidence OriginalPreflight(LifecycleExperimentContext context)
    {
        try
        {
            originalManifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
            var result = InspectRuntime(roots.OriginalGameRoot, "original-preflight");
            selectedExecutableRelativePath = result.SelectedExecutableRelativePath;
            return new(
                result.IsReadyForReversibleTest,
                result.SelectedExecutableRelativePath,
                result.GameFingerprint,
                result.IsReadyForReversibleTest,
                result.LoaderIndicatorsAbsent,
                result.IsReadyForReversibleTest ? null : LifecycleExperimentFailureCategories.OriginalPreflightFailed);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, null, null, false, false, LifecycleExperimentFailureCategories.OriginalPreflightFailed);
        }
    }

    public LifecycleStageEvidence DisposablePrepare(LifecycleExperimentContext context)
    {
        var result = RunLoaderMode(SmokeTestMode.Prepare, LifecycleExperimentFailureCategories.DisposablePrepareFailed);
        if (result.Succeeded && originalManifest is not null)
        {
            try
            {
                baseline = DisposableProfileBaselineService.LoadAndValidateSavedBaseline(
                    LoaderSmokeTestStatePaths.GetBaselinePath(roots),
                    options.ExpectedFingerprint,
                    originalManifest);
            }
            catch (Exception exception) when (IsSanitizedExternalFailure(exception))
            {
                return new(false, LifecycleExperimentFailureCategories.DisposablePrepareFailed);
            }
        }

        return result;
    }

    public LifecycleStageEvidence BaselineLaunch(LifecycleExperimentContext context)
    {
        var result = RunLoaderMode(SmokeTestMode.Baseline, LifecycleExperimentFailureCategories.BaselineLaunchFailed);
        return result;
    }

    public LifecycleStageEvidence LoaderInstall(LifecycleExperimentContext context)
    {
        var result = RunLoaderMode(SmokeTestMode.Install, LifecycleExperimentFailureCategories.LoaderInstallFailed);
        if (!result.Succeeded)
        {
            return result;
        }

        try
        {
            var evidence = LoaderStageVerificationService.Verify(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.Applied);
            loaderOnlyManifest = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
            return new(true, LoaderTransactionStatus: evidence.LoaderStatus, LoaderApplied: true);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderTransactionMissing);
        }
    }

    public LifecycleStageEvidence LoaderLaunch(LifecycleExperimentContext context)
    {
        try
        {
            _ = LoaderStageVerificationService.Verify(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.Applied);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderLaunchFailed);
        }

        var result = RunLoaderMode(SmokeTestMode.Launch, LifecycleExperimentFailureCategories.LoaderLaunchFailed);
        if (!result.Succeeded)
        {
            return result;
        }

        try
        {
            var evidence = LoaderStageVerificationService.Verify(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.LaunchObserved);
            return new(true, LoaderTransactionStatus: evidence.LoaderStatus, LoaderApplied: true);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderLaunchFailed);
        }
    }

    public LoaderVerificationEvidence LoaderVerify(LifecycleExperimentContext context)
    {
        try
        {
            _ = LoaderStageVerificationService.Verify(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.LaunchObserved);
            var result = RunLoaderMode(SmokeTestMode.Verify, LifecycleExperimentFailureCategories.LoaderVerifyFailed);
            if (!result.Succeeded)
            {
                return new(false, null, false, false, false, result.FailureCategory);
            }

            var evidence = LoaderStageVerificationService.Verify(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.LaunchObserved);
            return new(true, evidence.LoaderStatus, evidence.TransactionBaselineMatched, evidence.AppliedProfileMatched, evidence.BootstrapCriteria);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, null, false, false, false, exception.FailureCategory);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, null, false, false, false, LifecycleExperimentFailureCategories.LoaderVerifyFailed);
        }
    }

    public UnityMetadataEvidence UnityMetadataPreflight(LifecycleExperimentContext context)
    {
        try
        {
            var result = UnityLifecycleMetadataInspector.Inspect(options.UnityAssemblyPath);
            return new(result.IsValid, result.AssemblyIdentity, result.IsValid ? null : LifecycleExperimentFailureCategories.UnityMetadataPreflightFailed);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, null, LifecycleExperimentFailureCategories.UnityMetadataPreflightFailed);
        }
    }

    public PackageEvidence PackageBuild(LifecycleExperimentContext context)
    {
        try
        {
            var manifest = LifecyclePluginPackageService.CreateManifestFromDirectory(options.PackageRoot);
            PluginPackageManifestService.Save(options.ManifestPath, manifest);
            return new(true, manifest.PackageSha256.Value);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, null, LifecycleExperimentFailureCategories.PackageBuildFailed);
        }
    }

    public PackageEvidence PackageCapture(LifecycleExperimentContext context)
    {
        try
        {
            var expected = PluginPackageManifestService.Load(options.ManifestPath);
            capturedPackage = LifecyclePluginPackageService.CaptureAndValidate(options.PackageRoot, expected);
            packageDigest = capturedPackage.Manifest.PackageSha256.Value;
            return new(true, packageDigest);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, null, LifecycleExperimentFailureCategories.PackageCaptureFailed);
        }
    }

    public DeploymentEvidence AdmitAndDeploy(LifecycleExperimentContext context)
    {
        if (capturedPackage is null)
        {
            return new(false, LifecycleExperimentFailureCategories.PackageCaptureFailed);
        }

        try
        {
            var gameFingerprint = new GameFingerprint(options.ExpectedFingerprint);
            var decision = PluginAdmissionService.EvaluateApprovedPackage(
                capturedPackage.Manifest,
                new PluginAdmissionInputs(gameFingerprint, options.AdapterId, options.AdapterVersion, DateTimeOffset.UtcNow));
            if (decision.Status != CodeModAdmissionStatus.Approved || decision.Binding is null)
            {
                return new(false, LifecycleExperimentFailureCategories.AdmissionFailed);
            }

            admissionBindingDigest = decision.Binding.BindingDigest;
            var contextData = PluginDeploymentService.DeriveContext(
                roots.OriginalGameRoot,
                roots.CleanGameRoot,
                roots.ExperimentRoot,
                roots.RepositoryRoot,
                options.ExpectedFingerprint,
                decision.Binding);
            var receipt = PluginDeploymentService.DeployCaptured(capturedPackage, contextData);
            var state = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint) with
            {
                Status = Task6ExperimentStatus.PluginDeployed,
                PackageSha256 = receipt.PackageSha256,
                AdmissionBindingDigest = receipt.AdmissionBindingDigest,
                PluginRelativeRoot = receipt.RelativeRoot,
                LoaderTransactionStatus = contextData.LoaderTransaction.Status.ToString()
            };
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, state);
            return new(true, PackageSha256: receipt.PackageSha256, AdmissionBindingDigest: receipt.AdmissionBindingDigest, PluginDeployed: true);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory);
        }
        catch (PluginDeploymentVerificationException)
        {
            return new(false, LifecycleExperimentFailureCategories.DeploymentVerificationFailed);
        }
        catch (PluginSmokeException)
        {
            return new(false, LifecycleExperimentFailureCategories.DeploymentWriteFailed);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.DeploymentWriteFailed);
        }
    }

    public LifecycleStageEvidence LifecycleLaunch(LifecycleExperimentContext context)
    {
        try
        {
            var executableRelativePath = string.IsNullOrWhiteSpace(selectedExecutableRelativePath)
                ? options.ExecutableRelativePath
                : selectedExecutableRelativePath;
            if (string.IsNullOrWhiteSpace(executableRelativePath))
            {
                return new(false, LifecycleExperimentFailureCategories.LifecycleLaunchFailed);
            }

            var executable = Path.Combine(roots.CleanGameRoot, executableRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var result = LaunchObservationService.Observe(
                executable,
                roots.CleanGameRoot,
                roots.ExperimentRoot,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(10),
                new Dictionary<string, string>(StringComparer.Ordinal) { ["THRONEFORGE_SMOKE_NONCE"] = options.Nonce });
            if (result.RequiresManualClosure)
            {
                return new(false, LifecycleExperimentFailureCategories.ManualClosureRequired, ProcessActive: true);
            }

            return result.Started && result.Exited
                ? new(true)
                : new(false, LifecycleExperimentFailureCategories.LifecycleLaunchFailed);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LifecycleLaunchFailed);
        }
    }

    public LogStabilityEvidence LogStability(LifecycleExperimentContext context)
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(roots.CleanGameRoot, "BepInEx", "LogOutput.log"),
                Path.Combine(roots.CleanGameRoot, "BepInEx", "LogOutput.txt")
            }.Where(path => File.Exists(path)).ToArray();
            if (candidates.Length == 0)
            {
                return new(false, null, LifecycleExperimentFailureCategories.LogMissing);
            }

            if (candidates.Length != 1)
            {
                return new(false, null, LifecycleExperimentFailureCategories.LogNotReadable);
            }

            var stable = LifecycleLogStabilityObserver.Observe(candidates[0], TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));
            if (!stable.IsStable || stable.Text is null)
            {
                return new(false, null, stable.FailureCategory ?? LifecycleExperimentFailureCategories.LogNotStable);
            }

            lifecycleLog = stable.Text;
            return new(true, lifecycleLog);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, null, LifecycleExperimentFailureCategories.LogNotReadable);
        }
    }

    public LifecycleVerificationEvidence LifecycleVerification(LifecycleExperimentContext context)
    {
        if (lifecycleLog is null)
        {
            return new(false, LifecycleExperimentFailureCategories.LogMissing);
        }

        try
        {
            var loader = LoaderLogParser.Parse(lifecycleLog);
            var marker = LifecycleMarkerParser.Parse(
                lifecycleLog,
                options.Nonce,
                $"ThroneForge.API, Version=1.0.0.0",
                $"ThroneForge.Contracts, Version=1.0.0.0");
            if (!marker.IsValid)
            {
                return new(false, marker.FailureCategory ?? LifecycleExperimentFailureCategories.LifecycleMarkerInvalid);
            }

            return new LifecycleVerificationEvidence(
                loader.BepInExVersion == "5.4.23.5" && loader.PreloaderInitialized && loader.ChainloaderInitialized && loader.PluginsDiscovered == 1 && loader.ErrorCount == 0 && loader.FatalErrorCount == 0,
                LifecycleExperimentFailureCategories.LifecycleMarkerInvalid,
                marker.InitializationCount,
                marker.QuittingCount,
                marker.ShutdownCount,
                string.Join(',', marker.Markers.Select(item => item.Sequence)),
                marker.Markers[0].ApiIdentity,
                marker.Markers[0].ContractsIdentity,
                loader.PluginsDiscovered,
                loader.WarningCount,
                loader.ErrorCount,
                loader.FatalErrorCount) with
            {
                FailureCategory = loader.BepInExVersion == "5.4.23.5" && loader.PreloaderInitialized && loader.ChainloaderInitialized && loader.PluginsDiscovered == 1 && loader.ErrorCount == 0 && loader.FatalErrorCount == 0
                    ? null
                    : LifecycleExperimentFailureCategories.LifecycleMarkerInvalid
            };
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LifecycleMarkerInvalid);
        }
    }

    public CleanupEvidence PluginRemoval(LifecycleExperimentContext context)
    {
        try
        {
            PluginDeploymentService.Remove(roots.CleanGameRoot, LifecyclePluginPackageService.PluginGuid);
            var pluginPath = Path.Combine(roots.CleanGameRoot, "BepInEx", "plugins", LifecyclePluginPackageService.PluginGuid);
            var removal = !Directory.Exists(pluginPath);
            var loaderOnly = loaderOnlyManifest is not null && InstallationCopyService.CompareManifests(loaderOnlyManifest, InstallationCopyService.CaptureManifest(roots.CleanGameRoot)).Matches;
            if (!removal || !loaderOnly)
            {
                return new(false, LifecycleExperimentFailureCategories.PluginRemovalFailed, removal, loaderOnly);
            }

            var state = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint) with
            {
                Status = Task6ExperimentStatus.LoaderApplied,
                PluginRelativeRoot = null
            };
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, state);
            return new(true, null, true, true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.PluginRemovalFailed);
        }
    }

    public CleanupEvidence LoaderRollback(LifecycleExperimentContext context)
    {
        try
        {
            var result = RunLoaderMode(SmokeTestMode.Rollback, LifecycleExperimentFailureCategories.LoaderRollbackFailed);
            return new(result.Succeeded, result.FailureCategory, RollbackVerified: result.Succeeded);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderRollbackFailed);
        }
    }

    public PostcheckEvidence DisposablePostcheck(LifecycleExperimentContext context)
        => VerifyProfilePostcheck(roots.CleanGameRoot, baseline?.DisposableManifest, LifecycleExperimentFailureCategories.DisposableRestorationFailed);

    public PostcheckEvidence OriginalPostcheck(LifecycleExperimentContext context)
    {
        try
        {
            var current = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
            var manifestMatches = originalManifest is not null && InstallationCopyService.CompareManifests(originalManifest, current).Matches;
            var runtime = InspectRuntime(roots.OriginalGameRoot, "original-postcheck");
            var passed = manifestMatches && runtime.IsReadyForReversibleTest && runtime.LoaderIndicatorsAbsent;
            return new(passed, passed ? null : LifecycleExperimentFailureCategories.OriginalPostcheckFailed, manifestMatches, runtime.IsReadyForReversibleTest, runtime.LoaderIndicatorsAbsent, true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.OriginalPostcheckFailed, false, false, false, false);
        }
    }

    private PostcheckEvidence VerifyProfilePostcheck(string profileRoot, CopyManifest? expectedManifest, string failureCategory)
    {
        try
        {
            if (expectedManifest is null)
            {
                return new(false, failureCategory, false, false, false, false);
            }

            var manifestMatches = InstallationCopyService.CompareManifests(expectedManifest, InstallationCopyService.CaptureManifest(profileRoot)).Matches;
            var runtime = InspectRuntime(profileRoot, "disposable-postcheck");
            var passed = manifestMatches && runtime.IsReadyForReversibleTest && runtime.LoaderIndicatorsAbsent;
            return new(passed, passed ? null : failureCategory, manifestMatches, runtime.IsReadyForReversibleTest, runtime.LoaderIndicatorsAbsent, manifestMatches);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, failureCategory, false, false, false, false);
        }
    }

    private RuntimeCompatibilityEvidence InspectRuntime(string gameRoot, string evidenceName)
    {
        var outputRoot = Path.Combine(roots.EvidenceRoot, evidenceName);
        Directory.CreateDirectory(outputRoot);
        var result = new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(gameRoot, options.ExpectedFingerprint, outputRoot, true));
        return RuntimeCompatibilityEvidenceContract.Parse(RuntimeCompatibilityEvidenceContract.Serialize(result), options.ExpectedFingerprint);
    }

    private LifecycleStageEvidence RunLoaderMode(SmokeTestMode mode, string failureCategory)
    {
        try
        {
            var result = SmokeTestOrchestrator.Run(new LoaderSmokeTestRequest(
                mode,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                roots.RepositoryRoot,
                options.BepInExArchivePath,
                null,
                OfficialAssetDigest: options.ExpectedBepInExDigest));
            var succeeded = mode is SmokeTestMode.Prepare or SmokeTestMode.Install
                ? result.Outcome is not SmokeTestOutcome.Failed
                : mode == SmokeTestMode.Baseline
                    ? result.Outcome == SmokeTestOutcome.Passed
                    : result.Outcome is SmokeTestOutcome.Passed or SmokeTestOutcome.PassedWithWarnings;
            if (!succeeded)
            {
                return new(false, failureCategory);
            }

            return new(true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, failureCategory);
        }
    }

    private static bool IsSanitizedExternalFailure(Exception exception)
        => exception is PluginSmokeException
            or SmokeTestException
            or DiscoveryException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException;
}

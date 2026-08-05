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
    string AdapterVersion = "1.0.0",
    string RepositoryBaselineCommit = "unknown",
    string DotnetPath = "dotnet",
    Func<SmokeTestMode, SmokeTestExecutionResult>? LoaderModeRunner = null,
    Func<SmokeTestRoots, LoaderTransactionStatus, LoaderStageVerificationEvidence>? LoaderStateVerifier = null);

/// <summary>
/// Production adapter for the real Task-7 CLI. It calls the existing Task-3/Task-6 services and
/// returns typed evidence to the orchestrator; it does not interpret stages or write Markdown.
/// </summary>
public sealed class LifecycleExperimentProductionOperations : ILifecycleExperimentOperations
{
    private readonly LifecycleExperimentProductionOptions options;
    private readonly SmokeTestRoots roots;
    private readonly string packageBuildRoot;
    private readonly ILifecyclePluginPackageBuilder packageBuilder;
    private CopyManifest? originalManifest;
    private CopyManifest? loaderOnlyManifest;
    private DisposableProfileBaseline? baseline;
    private CapturedPluginPackage? capturedPackage;
    private string? lifecycleLog;
    private string? packageDigest;
    private string? admissionBindingDigest;
    private string? selectedExecutableRelativePath;
    private string? expectedApiIdentity;
    private string? expectedContractsIdentity;
    private bool disposableRestored;

    public LifecycleExperimentProductionOperations(
        LifecycleExperimentProductionOptions options,
        ILifecyclePluginPackageBuilder? packageBuilder = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        roots = SmokeTestPathValidator.ValidateRoots(options.RepositoryRoot, options.OriginalGameRoot, options.ExperimentRoot);
        _ = SmokeTestPathValidator.ValidateOwnedExperimentDirectory(roots, options.PackageRoot, "the package root");
        _ = SmokeTestPathValidator.ValidateOwnedExperimentFile(roots, options.ManifestPath, "the package manifest");
        _ = SmokeTestPathValidator.ValidateUnityAssemblyPath(roots, options.UnityAssemblyPath, options.ExecutableRelativePath);
        packageBuildRoot = SmokeTestPathValidator.ValidateOwnedExperimentDirectory(
            roots,
            Path.Combine(roots.ExperimentRoot, "package-build"),
            "the package build root");
        if (string.IsNullOrWhiteSpace(options.DotnetPath))
        {
            throw new SmokeTestException("The .NET executable path is required for the lifecycle package build.");
        }

        this.packageBuilder = packageBuilder ?? new SourceLifecyclePluginPackageBuilder();
    }

    public LifecycleStageEvidence EnsureOwnership(LifecycleExperimentContext context)
    {
        try
        {
            var statePath = Task6ExperimentStateService.GetStatePath(roots.ExperimentRoot);
            if (File.Exists(statePath))
            {
                return new(false, LifecycleExperimentFailureCategories.OwnershipStateInvalid);
            }

            var state = Task6ExperimentStateService.CreatePrepared(
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                options.RepositoryBaselineCommit,
                context.ExperimentId);
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, state);
            return new(true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.OwnershipStateInvalid);
        }
    }

    public RecoveryEvidence PersistManualClosureRecovery(LifecycleExperimentContext context)
    {
        var markerPersisted = false;
        try
        {
            var ownership = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint);
            var loaderApplied = ownership.Status is Task6ExperimentStatus.LoaderApplied
                or Task6ExperimentStatus.PluginDeployed
                or Task6ExperimentStatus.LaunchObserved
                || ownership.LoaderTransactionStatus is not null
                    && (ownership.LoaderTransactionStatus.Equals(LoaderTransactionStatus.Applied.ToString(), StringComparison.Ordinal)
                        || ownership.LoaderTransactionStatus.Equals(LoaderTransactionStatus.LaunchObserved.ToString(), StringComparison.Ordinal)
                        || ownership.LoaderTransactionStatus.Equals(LoaderTransactionStatus.RollbackRequired.ToString(), StringComparison.Ordinal));
            var loaderStatus = loaderApplied
                ? ownership.LoaderTransactionStatus ?? LoaderTransactionStatus.RollbackRequired.ToString()
                : "NotApplied";
            var recoveryAction = loaderApplied ? "rollback-lifecycle-experiment" : "no-loader-cleanup-required";
            var recovery = new Task6RecoveryState(
                Task6ExperimentStateService.SchemaVersion,
                Task6ExperimentStateService.TaskVersion,
                options.ExpectedFingerprint.ToLowerInvariant(),
                ownership.ExperimentId,
                packageDigest ?? ownership.PackageSha256,
                admissionBindingDigest ?? ownership.AdmissionBindingDigest,
                ownership.PluginRelativeRoot,
                loaderStatus,
                "ManualClosureRequired");
            Task6ExperimentStateService.SaveRecoveryAtomic(roots.ExperimentRoot, recovery);
            markerPersisted = true;
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, ownership with
            {
                Status = Task6ExperimentStatus.ManualClosureRequired,
                PackageSha256 = recovery.PackageSha256,
                AdmissionBindingDigest = recovery.AdmissionBindingDigest,
                PluginRelativeRoot = recovery.PluginRelativeRoot,
                LoaderTransactionStatus = recovery.LoaderTransactionStatus
            });
            return loaderApplied
                ? new(true, true, RollbackCommand: recoveryAction, RecoveryAction: recoveryAction)
                : new(true, true, RecoveryAction: recoveryAction);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, markerPersisted, LifecycleExperimentFailureCategories.ManualClosureRequired);
        }
    }

    public LifecycleStageEvidence FinalizeFailure(LifecycleExperimentContext context)
    {
        try
        {
            var statePath = Task6ExperimentStateService.GetStatePath(roots.ExperimentRoot);
            if (!File.Exists(statePath))
            {
                return new(true);
            }

            var state = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint);
            if (state.Status is Task6ExperimentStatus.Completed or Task6ExperimentStatus.ManualClosureRequired)
            {
                return new(true);
            }

            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, state with { Status = Task6ExperimentStatus.Failed });
            return new(true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.OwnershipStateInvalid);
        }
    }

    public OriginalPreflightEvidence OriginalPreflight(LifecycleExperimentContext context)
    {
        try
        {
            originalManifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
            var result = InspectRuntime(roots.OriginalGameRoot, "original-preflight");
            selectedExecutableRelativePath = result.SelectedExecutableRelativePath;
            if (!RelativePathsEqual(result.SelectedExecutableRelativePath, options.ExecutableRelativePath))
            {
                return new(
                    false,
                    result.SelectedExecutableRelativePath,
                    result.GameFingerprint,
                    result.IsReadyForReversibleTest,
                    result.LoaderIndicatorsAbsent,
                    LifecycleExperimentFailureCategories.ExecutableBindingMismatch);
            }

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
            return result with { LoaderApplied = InferLoaderMayRequireRollback() };
        }

        try
        {
            var ownership = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint) with
            {
                Status = Task6ExperimentStatus.LoaderApplied,
                LoaderTransactionStatus = LoaderTransactionStatus.Applied.ToString()
            };
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, ownership);
            var evidence = VerifyLoaderStage(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.Applied);
            return new(true, LoaderTransactionStatus: evidence.LoaderStatus, LoaderApplied: true);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory, LoaderApplied: true, LoaderTransactionStatus: LoaderTransactionStatus.Applied.ToString());
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderTransactionMissing, LoaderApplied: true, LoaderTransactionStatus: LoaderTransactionStatus.Applied.ToString());
        }
    }

    public LifecycleStageEvidence LoaderLaunch(LifecycleExperimentContext context)
    {
        try
        {
            _ = VerifyLoaderStage(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.Applied);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory, LoaderApplied: true, LoaderTransactionStatus: LoaderTransactionStatus.Applied.ToString());
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderLaunchFailed, LoaderApplied: true, LoaderTransactionStatus: LoaderTransactionStatus.Applied.ToString());
        }

        var result = RunLoaderMode(SmokeTestMode.Launch, LifecycleExperimentFailureCategories.LoaderLaunchFailed);
        if (!result.Succeeded)
        {
            return result with { LoaderApplied = true };
        }

        try
        {
            var ownership = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint) with
            {
                Status = Task6ExperimentStatus.LaunchObserved,
                LoaderTransactionStatus = LoaderTransactionStatus.LaunchObserved.ToString()
            };
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, ownership);
            var evidence = VerifyLoaderStage(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.LaunchObserved);
            return new(true, LoaderTransactionStatus: evidence.LoaderStatus, LoaderApplied: true);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory, LoaderApplied: true, LoaderTransactionStatus: LoaderTransactionStatus.LaunchObserved.ToString());
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderLaunchFailed, LoaderApplied: true, LoaderTransactionStatus: LoaderTransactionStatus.LaunchObserved.ToString());
        }
    }

    public LoaderVerificationEvidence LoaderVerify(LifecycleExperimentContext context)
    {
        try
        {
            _ = VerifyLoaderStage(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.LaunchObserved);
            var result = RunLoaderMode(SmokeTestMode.Verify, LifecycleExperimentFailureCategories.LoaderVerifyFailed);
            if (!result.Succeeded)
            {
                return new(false, null, false, false, false, result.FailureCategory, true);
            }

            var evidence = VerifyLoaderStage(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                options.ExpectedFingerprint,
                LoaderTransactionStatus.LaunchObserved);
            return new(true, evidence.LoaderStatus, evidence.TransactionBaselineMatched, evidence.AppliedProfileMatched, evidence.BootstrapCriteria);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, null, false, false, false, exception.FailureCategory, true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, null, false, false, false, LifecycleExperimentFailureCategories.LoaderVerifyFailed, true);
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
            packageBuilder.Build(new LifecyclePluginPackageBuildRequest(
                roots.RepositoryRoot,
                roots.CleanGameRoot,
                packageBuildRoot,
                options.PackageRoot,
                options.UnityAssemblyPath,
                options.DotnetPath));
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
            expectedApiIdentity = capturedPackage.Metadata["ThroneForge.API.dll"].AssemblyIdentity;
            expectedContractsIdentity = capturedPackage.Metadata["ThroneForge.Contracts.dll"].AssemblyIdentity;
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

        var deployed = false;
        try
        {
            loaderOnlyManifest = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
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
            deployed = true;
            var state = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint) with
            {
                Status = Task6ExperimentStatus.PluginDeployed,
                PackageSha256 = receipt.PackageSha256,
                AdmissionBindingDigest = receipt.AdmissionBindingDigest,
                PluginRelativeRoot = receipt.RelativeRoot,
                LoaderTransactionStatus = contextData.LoaderTransaction.Status.ToString(),
                LoaderOnlyManifest = loaderOnlyManifest
            };
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, state);
            return new(true, PackageSha256: receipt.PackageSha256, AdmissionBindingDigest: receipt.AdmissionBindingDigest, PluginDeployed: true);
        }
        catch (PluginSmokeStateException exception)
        {
            return new(false, exception.FailureCategory, packageDigest, admissionBindingDigest, deployed);
        }
        catch (PluginDeploymentVerificationException)
        {
            return new(false, LifecycleExperimentFailureCategories.DeploymentVerificationFailed, packageDigest, admissionBindingDigest, deployed);
        }
        catch (PluginSmokeException)
        {
            return new(false, deployed ? LifecycleExperimentFailureCategories.DeploymentVerificationFailed : LifecycleExperimentFailureCategories.DeploymentWriteFailed, packageDigest, admissionBindingDigest, deployed);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, deployed ? LifecycleExperimentFailureCategories.DeploymentVerificationFailed : LifecycleExperimentFailureCategories.DeploymentWriteFailed, packageDigest, admissionBindingDigest, deployed);
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
            if (expectedApiIdentity is null || expectedContractsIdentity is null)
            {
                return new(false, LifecycleExperimentFailureCategories.PackageCaptureFailed);
            }

            var loader = LoaderLogParser.Parse(lifecycleLog);
            var marker = LifecycleMarkerParser.Parse(
                lifecycleLog,
                options.Nonce,
                expectedApiIdentity,
                expectedContractsIdentity);
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
            var ownership = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint);
            PluginDeploymentService.Remove(roots.CleanGameRoot, LifecyclePluginPackageService.PluginGuid);
            var pluginPath = Path.Combine(roots.CleanGameRoot, "BepInEx", "plugins", LifecyclePluginPackageService.PluginGuid);
            var removal = !Directory.Exists(pluginPath);
            var expectedLoaderOnly = loaderOnlyManifest ?? ownership.LoaderOnlyManifest;
            var loaderOnly = expectedLoaderOnly is not null && LoaderOnlyProfileVerificationService.Compare(expectedLoaderOnly, InstallationCopyService.CaptureManifest(roots.CleanGameRoot)).Matches;
            if (!removal || !loaderOnly)
            {
                return new(false, LifecycleExperimentFailureCategories.PluginRemovalFailed, removal, loaderOnly);
            }

            var state = ownership with
            {
                Status = Task6ExperimentStatus.LoaderApplied,
                PluginRelativeRoot = null,
                LoaderOnlyManifest = expectedLoaderOnly
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
            if (!result.Succeeded || baseline is null)
            {
                return new(false, result.FailureCategory ?? LifecycleExperimentFailureCategories.LoaderRollbackFailed, RollbackVerified: false);
            }

            var transaction = LoaderTransactionStateService.LoadAndValidate(
                LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                roots,
                options.ExpectedFingerprint,
                baseline.DisposableManifest,
                [LoaderTransactionStatus.RolledBack]);
            if (transaction.Status != LoaderTransactionStatus.RolledBack)
            {
                return new(false, LifecycleExperimentFailureCategories.LoaderRollbackFailed, RollbackVerified: false);
            }

            var state = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint) with
            {
                Status = Task6ExperimentStatus.RolledBack,
                LoaderTransactionStatus = LoaderTransactionStatus.RolledBack.ToString(),
                PluginRelativeRoot = null
            };
            Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, state);
            return new(true, null, RollbackVerified: true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new(false, LifecycleExperimentFailureCategories.LoaderRollbackFailed);
        }
    }

    public PostcheckEvidence DisposablePostcheck(LifecycleExperimentContext context)
    {
        var result = VerifyProfilePostcheck(roots.CleanGameRoot, baseline?.DisposableManifest, LifecycleExperimentFailureCategories.DisposableRestorationFailed);
        disposableRestored = result.Succeeded && result.RestorationVerified == true;
        return result;
    }

    public PostcheckEvidence OriginalPostcheck(LifecycleExperimentContext context)
    {
        try
        {
            var current = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
            var manifestMatches = originalManifest is not null && InstallationCopyService.CompareManifests(originalManifest, current).Matches;
            var runtime = InspectRuntime(roots.OriginalGameRoot, "original-postcheck");
            var passed = manifestMatches && runtime.IsReadyForReversibleTest && runtime.LoaderIndicatorsAbsent;
            if (passed && baseline is not null && disposableRestored)
            {
                var state = Task6ExperimentStateService.LoadAndValidate(roots.ExperimentRoot, options.ExpectedFingerprint) with
                {
                    Status = Task6ExperimentStatus.Completed
                };
                Task6ExperimentStateService.SaveAtomic(roots.ExperimentRoot, state);
                Task6ExperimentStateService.ClearRecovery(roots.ExperimentRoot);
            }
            return new(passed, passed ? null : LifecycleExperimentFailureCategories.OriginalPostcheckFailed, manifestMatches, runtime.IsReadyForReversibleTest, runtime.LoaderIndicatorsAbsent, passed);
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
            return new(passed, passed ? null : failureCategory, manifestMatches, runtime.IsReadyForReversibleTest, runtime.LoaderIndicatorsAbsent, passed);
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

    private LoaderStageVerificationEvidence VerifyLoaderStage(
        string repositoryRoot,
        string originalGameRoot,
        string experimentRoot,
        string expectedFingerprint,
        LoaderTransactionStatus expectedStatus)
        => options.LoaderStateVerifier?.Invoke(roots, expectedStatus)
            ?? LoaderStageVerificationService.Verify(
                repositoryRoot,
                originalGameRoot,
                experimentRoot,
                expectedFingerprint,
                expectedStatus);

    private LoaderModeExecutionEvidence RunLoaderMode(SmokeTestMode mode, string failureCategory)
    {
        try
        {
            var result = options.LoaderModeRunner?.Invoke(mode)
                ?? SmokeTestOrchestrator.Run(new LoaderSmokeTestRequest(
                    mode,
                    roots.OriginalGameRoot,
                    roots.ExperimentRoot,
                    options.ExpectedFingerprint,
                    roots.RepositoryRoot,
                    options.BepInExArchivePath,
                    null,
                    OfficialAssetDigest: options.ExpectedBepInExDigest));
            var processActive = result.LaunchObservation?.RequiresManualClosure == true
                && result.LaunchObservation.Exited == false;
            if (processActive)
            {
                return new LoaderModeExecutionEvidence(
                    false,
                    LifecycleExperimentFailureCategories.ManualClosureRequired,
                    ActiveProcess: true,
                    RequiresManualClosure: true);
            }

            var succeeded = mode is SmokeTestMode.Prepare or SmokeTestMode.Install
                ? result.Outcome is not SmokeTestOutcome.Failed
                : mode == SmokeTestMode.Baseline
                    ? result.Outcome == SmokeTestOutcome.Passed
                    : result.Outcome is SmokeTestOutcome.Passed or SmokeTestOutcome.PassedWithWarnings;
            if (!succeeded)
            {
                return new LoaderModeExecutionEvidence(false, failureCategory);
            }

            return new LoaderModeExecutionEvidence(true);
        }
        catch (Exception exception) when (IsSanitizedExternalFailure(exception))
        {
            return new LoaderModeExecutionEvidence(false, failureCategory);
        }
    }

    private bool InferLoaderMayRequireRollback()
    {
        var transactionPath = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        if (!File.Exists(transactionPath))
        {
            // A missing state after an attempted mutation is not proof that no files changed.
            return true;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(transactionPath));
            if (!document.RootElement.TryGetProperty("Status", out var status))
            {
                return true;
            }

            var value = status.GetString();
            return !string.Equals(value, LoaderTransactionStatus.FailedAndRolledBack.ToString(), StringComparison.Ordinal)
                && !string.Equals(value, LoaderTransactionStatus.RolledBack.ToString(), StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return true;
        }
    }

    private static bool RelativePathsEqual(string? left, string right)
        => left is not null
            && string.Equals(
                left.Replace('\\', '/'),
                right.Replace('\\', '/'),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsSanitizedExternalFailure(Exception exception)
        => exception is PluginSmokeException
            or SmokeTestException
            or DiscoveryException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException;
}

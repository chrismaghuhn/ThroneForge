using System.Text.Json;
using ThroneForge.LoaderSmokeTest;
using ThroneForge.Runtime;

namespace ThroneForge.PluginSmokeTest;

internal static class Program
{
    public static int Main(string[] args) => PluginSmokeCli.Run(args, Console.Out, Console.Error);
}

public static class PluginSmokeCli
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine("Usage: package|lifecycle-package|admit|admit-and-deploy|verify-loader-stage|run-lifecycle-experiment|rollback-lifecycle-experiment|remove|parse-marker|inspect-lifecycle-binding|verify-lifecycle-log|lifecycle-stage with explicit paths and evidence.");
            return 2;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "package" => Package(args, stdout),
                "lifecycle-package" => LifecyclePackage(args, stdout),
                "admit" => Admit(args, stdout),
                "admit-and-deploy" => AdmitAndDeploy(args, stdout),
                "verify-loader-stage" => VerifyLoaderStage(args, stdout),
                "run-lifecycle-experiment" => RunLifecycleExperiment(args, stdout),
                "rollback-lifecycle-experiment" => RollbackLifecycleExperiment(args, stdout),
                "deploy" => throw new PluginSmokeException("Direct deployment is disabled; use admit-and-deploy with a validated Task-6 ownership record."),
                "remove" => Remove(args, stdout),
                "parse-marker" => ParseMarker(args, stdout),
                "inspect" => Inspect(args, stdout),
                "inspect-lifecycle-binding" => InspectLifecycleBinding(args, stdout),
                "tfm" => Tfm(args, stdout),
                "launch" => Launch(args, stdout),
                "verify-log" => VerifyLog(args, stdout),
                "verify-lifecycle-log" => VerifyLifecycleLog(args, stdout),
                "manifest" => Manifest(args, stdout),
                "ownership" => Ownership(args, stdout),
                "recovery" => Recovery(args, stdout),
                "cleanup-owned" => CleanupOwned(args, stdout),
                "lifecycle-stage" => LifecycleStage(args, stdout),
                _ => throw new PluginSmokeException("The requested plugin smoke-test operation is unsupported.")
            };
        }
        catch (PluginSmokePhaseException exception)
        {
            stderr.WriteLine($"Plugin smoke test failed: phase={exception.Phase}; phase-failure-category={exception.FailureCategory}.");
            return 2;
        }
        catch (PluginSmokeStateException exception)
        {
            stderr.WriteLine($"Plugin smoke test failed: state-failure-category={exception.FailureCategory}.");
            return 2;
        }
        catch (PluginSmokeException exception)
        {
            stderr.WriteLine($"Plugin smoke test failed: {exception.Message}");
            return 2;
        }
        catch (ArgumentException exception)
        {
            stderr.WriteLine($"Invalid plugin smoke-test arguments: {exception.Message}");
            return 2;
        }
        catch (ThroneForge.LoaderSmokeTest.SmokeTestException)
        {
            stderr.WriteLine("Plugin smoke test failed: the disposable profile state could not be validated safely.");
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            stderr.WriteLine("Plugin smoke test failed: the requested evidence could not be read safely.");
            return 2;
        }
    }

    private static int Package(string[] args, TextWriter stdout)
    {
        var packageRoot = Value(args, "--package-root");
        var manifestPath = Value(args, "--manifest-path");
        var targetFramework = Value(args, "--target-framework");
        var identity = new ThroneForge.Contracts.ModIdentity(
            "dev.throneforge.m1.synthetic-smoke",
            "0.0.1");
        var manifest = PluginPackageManifestService.CreateFromDirectory(
            packageRoot,
            identity,
            [
                "ThroneForge.M1.SyntheticSmoke.dll",
                "ThroneForge.API.dll",
                "ThroneForge.Contracts.dll"
            ],
            targetFramework);
        PluginPackageManifestService.Save(manifestPath, manifest);
        stdout.WriteLine($"package-sha256={manifest.PackageSha256.Value}");
        stdout.WriteLine("package-file-count=3");
        return 0;
    }

    private static int LifecyclePackage(string[] args, TextWriter stdout)
    {
        var manifest = LifecyclePluginPackageService.CreateManifestFromDirectory(Value(args, "--package-root"));
        PluginPackageManifestService.Save(Value(args, "--manifest-path"), manifest);
        stdout.WriteLine($"package-sha256={manifest.PackageSha256.Value}");
        stdout.WriteLine("package-file-count=3");
        return 0;
    }

    private static int Admit(string[] args, TextWriter stdout)
    {
        var expected = PluginPackageManifestService.Load(Value(args, "--manifest-path"));
        var captured = IsLifecycle(args)
            ? LifecyclePluginPackageService.CaptureAndValidate(Value(args, "--package-root"), expected)
            : PluginAdmissionService.CaptureAndValidate(Value(args, "--package-root"), expected, Value(args, "--target-framework"));
        var fingerprint = new ThroneForge.Contracts.GameFingerprint(Value(args, "--expected-fingerprint"));
        var decision = PluginAdmissionService.EvaluateApprovedPackage(
            captured.Manifest,
            new PluginAdmissionInputs(
                fingerprint,
                Value(args, "--adapter-id"),
                Value(args, "--adapter-version"),
                DateTimeOffset.UtcNow));
        stdout.WriteLine($"admission={decision.Status}");
        stdout.WriteLine($"reason={decision.ReasonCode}");
        stdout.WriteLine($"package-sha256={captured.Manifest.PackageSha256.Value}");
        if (decision.Binding is not null)
        {
            stdout.WriteLine($"binding-digest={decision.Binding.BindingDigest}");
        }

        return decision.Status == ThroneForge.Runtime.CodeModAdmissionStatus.Approved ? 0 : 1;
    }

    private static int AdmitAndDeploy(string[] args, TextWriter stdout)
    {
        PluginPackageManifest expected;
        try
        {
            expected = PluginPackageManifestService.Load(Value(args, "--manifest-path"));
        }
        catch (Exception exception)
        {
            throw new PluginSmokePhaseException("package-capture", LifecycleExperimentFailureCategories.PackageCaptureFailed, "The expected package manifest could not be loaded safely.", exception);
        }
        var targetFramework = Value(args, "--target-framework");
        CapturedPluginPackage captured;
        try
        {
            captured = IsLifecycle(args)
                ? LifecyclePluginPackageService.CaptureAndValidate(Value(args, "--package-root"), expected)
                : PluginAdmissionService.CaptureAndValidate(Value(args, "--package-root"), expected, targetFramework);
        }
        catch (Exception exception)
        {
            throw new PluginSmokePhaseException("metadata-validation", LifecycleExperimentFailureCategories.MetadataValidationFailed, "The captured package did not pass metadata validation.", exception);
        }
        var fingerprint = new ThroneForge.Contracts.GameFingerprint(Value(args, "--expected-fingerprint"));
        CodeModAdmissionDecision decision;
        try
        {
            decision = PluginAdmissionService.EvaluateApprovedPackage(
                captured.Manifest,
                new PluginAdmissionInputs(fingerprint, Value(args, "--adapter-id"), Value(args, "--adapter-version"), DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            throw new PluginSmokePhaseException("admission", LifecycleExperimentFailureCategories.AdmissionFailed, "The captured package could not be evaluated by the admission gate.", exception);
        }
        if (decision.Status != ThroneForge.Runtime.CodeModAdmissionStatus.Approved || decision.Binding is null)
        {
            throw new PluginSmokePhaseException("admission", LifecycleExperimentFailureCategories.AdmissionFailed, "The current captured package did not pass the admission gate immediately before deployment.");
        }

        PluginDeploymentContext context;
        try
        {
            context = PluginDeploymentService.DeriveContext(
                Value(args, "--original-game"),
                Value(args, "--clean-game"),
                Value(args, "--experiment-root"),
                Value(args, "--repository-root"),
                fingerprint.Value,
                decision.Binding);
        }
        catch (Exception exception)
        {
            throw new PluginSmokePhaseException("deployment-context", LifecycleExperimentFailureCategories.DeploymentContextFailed, "The disposable deployment context could not be validated.", exception);
        }

        PluginDeploymentReceipt receipt;
        try
        {
            receipt = PluginDeploymentService.DeployCaptured(captured, context);
        }
        catch (PluginDeploymentVerificationException exception)
        {
            throw new PluginSmokePhaseException("deployment-verification", LifecycleExperimentFailureCategories.DeploymentVerificationFailed, "The complete post-deployment manifest could not be verified.", exception);
        }
        catch (Exception exception)
        {
            throw new PluginSmokePhaseException("deployment-write", LifecycleExperimentFailureCategories.DeploymentWriteFailed, "The package deployment did not complete transactionally.", exception);
        }
        var updated = Task6ExperimentStateService.LoadAndValidate(Value(args, "--experiment-root"), fingerprint.Value) with
        {
            Status = Task6ExperimentStatus.PluginDeployed,
            PackageSha256 = receipt.PackageSha256,
            AdmissionBindingDigest = receipt.AdmissionBindingDigest,
            PluginRelativeRoot = receipt.RelativeRoot,
            LoaderTransactionStatus = context.LoaderTransaction.Status.ToString()
        };
        Task6ExperimentStateService.SaveAtomic(Value(args, "--experiment-root"), updated);
        stdout.WriteLine($"admission=Approved");
        stdout.WriteLine($"package-sha256={receipt.PackageSha256}");
        stdout.WriteLine($"binding-digest={receipt.AdmissionBindingDigest}");
        stdout.WriteLine($"deployed-file-count={receipt.DeployedRelativePaths.Count}");
        stdout.WriteLine($"deployed-relative-paths={string.Join(',', receipt.DeployedRelativePaths)}");
        stdout.WriteLine($"deployed-sha256={string.Join(',', receipt.DeployedSha256)}");
        return 0;
    }

    private static int VerifyLoaderStage(string[] args, TextWriter stdout)
    {
        var expectedStatus = Enum.Parse<LoaderTransactionStatus>(Value(args, "--expected-status"), ignoreCase: false);
        if (expectedStatus is not (LoaderTransactionStatus.Applied or LoaderTransactionStatus.LaunchObserved))
        {
            throw new PluginSmokeException("Only Applied and LaunchObserved are valid loader-stage expectations.");
        }

        var evidence = LoaderStageVerificationService.Verify(
            Value(args, "--repository-root"),
            Value(args, "--original-game"),
            Value(args, "--experiment-root"),
            Value(args, "--expected-fingerprint"),
            expectedStatus);
        stdout.WriteLine($"loader-status={evidence.LoaderStatus}");
        stdout.WriteLine($"baseline-manifest-identity={evidence.BaselineManifestIdentity}");
        stdout.WriteLine($"transaction-baseline-matched={evidence.TransactionBaselineMatched}");
        stdout.WriteLine($"applied-profile-matched={evidence.AppliedProfileMatched}");
        stdout.WriteLine($"bootstrap-evidence-present={evidence.BootstrapEvidencePresent}");
        stdout.WriteLine($"bootstrap-criteria={evidence.BootstrapCriteria}");
        return 0;
    }

    private static int RunLifecycleExperiment(string[] args, TextWriter stdout)
    {
        var repositoryRoot = Value(args, "--repository-root");
        var originalGameRoot = Value(args, "--original-game");
        var experimentRoot = Value(args, "--experiment-root");
        var expectedFingerprint = Value(args, "--expected-fingerprint");
        var archivePath = Value(args, "--bepinex-archive");
        var officialDigest = Value(args, "--official-digest");
        var packageRoot = Value(args, "--package-root");
        var manifestPath = Value(args, "--manifest-path");
        var unityAssemblyPath = Value(args, "--unity-assembly");
        var repositoryBaselineCommit = Value(args, "--repository-baseline-commit");
        var dotnetPath = OptionalValue(args, "--dotnet-path") ?? Environment.ProcessPath ?? "dotnet";
        var nonce = OptionalValue(args, "--nonce") ?? Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        var options = new LifecycleExperimentProductionOptions(
            repositoryRoot,
            originalGameRoot,
            experimentRoot,
            expectedFingerprint,
            archivePath,
            officialDigest,
            packageRoot,
            manifestPath,
            unityAssemblyPath,
            Value(args, "--executable-relative-path"),
            nonce,
            RepositoryBaselineCommit: repositoryBaselineCommit,
            DotnetPath: dotnetPath);
        var operation = new LifecycleExperimentProductionOperations(options);
        var experimentId = OptionalValue(args, "--experiment-id") ?? Guid.NewGuid().ToString("N");
        var result = new LifecycleExperimentOrchestrator(
            experimentRoot,
            experimentId,
            expectedFingerprint,
            operation,
            repositoryBaselineCommit,
            Value(args, "--executable-relative-path")).Run();
        var reportPath = new LifecycleExperimentReportWriter(repositoryRoot, expectedFingerprint).Write(result);

        stdout.WriteLine($"result={result.OverallResult}");
        stdout.WriteLine($"current-stage={result.CurrentStage}");
        stdout.WriteLine($"failed-stage={result.FailedStage?.ToString() ?? "none"}");
        stdout.WriteLine($"last-completed-stage={result.LastCompletedStage?.ToString() ?? "none"}");
        stdout.WriteLine($"primary-failed-stage={result.PrimaryFailedStage?.ToString() ?? "none"}");
        stdout.WriteLine($"primary-failure-category={result.PrimaryFailureCategory ?? "none"}");
        stdout.WriteLine($"cleanup-failure-category={result.CleanupFailureCategory ?? "none"}");
        stdout.WriteLine($"stage-state-persisted={result.StageStatePersisted}");
        stdout.WriteLine($"package-sha256={result.PackageSha256 ?? "not-observed"}");
        stdout.WriteLine($"binding-digest={result.AdmissionBindingDigest ?? "not-observed"}");
        stdout.WriteLine($"recovery-marker-persisted={result.RecoveryMarkerPersisted?.ToString() ?? "not-observed"}");
        stdout.WriteLine($"rollback-operation={result.RollbackCommand ?? "not-observed"}");
        stdout.WriteLine($"recovery-action={result.RecoveryAction ?? "not-observed"}");
        stdout.WriteLine($"report={Path.GetFileName(reportPath)}");
        return result.OverallResult == "Passed" ? 0 : 1;
    }

    private static int RollbackLifecycleExperiment(string[] args, TextWriter stdout)
    {
        var result = LifecycleExperimentRecoveryService.Rollback(new LifecycleExperimentRecoveryOptions(
            Value(args, "--repository-root"),
            Value(args, "--original-game"),
            Value(args, "--experiment-root"),
            Value(args, "--expected-fingerprint"),
            Value(args, "--bepinex-archive"),
            Value(args, "--official-digest")));
        stdout.WriteLine($"rollback-result={result.OverallResult}");
        stdout.WriteLine($"loader-rollback-verified={result.LoaderRollbackVerified}");
        stdout.WriteLine($"disposable-restored={result.DisposableRestored}");
        stdout.WriteLine($"original-verified={result.OriginalVerified}");
        stdout.WriteLine($"plugin-removal-status={result.PluginRemovalStatus}");
        stdout.WriteLine($"loader-rollback-status={result.LoaderRollbackStatus}");
        stdout.WriteLine($"failure-category={result.FailureCategory ?? "none"}");
        var reportPath = new LifecycleExperimentReportWriter(
            Value(args, "--repository-root"),
            Value(args, "--expected-fingerprint")).AppendRecovery(result);
        stdout.WriteLine($"report={Path.GetFileName(reportPath)}");
        return result.OverallResult == "Passed" ? 0 : 1;
    }

    private static int Remove(string[] args, TextWriter stdout)
    {
        var experimentRoot = Value(args, "--experiment-root");
        var expectedFingerprint = Value(args, "--expected-fingerprint");
        var state = Task6ExperimentStateService.LoadAndValidate(experimentRoot, expectedFingerprint);
        if (state.Status is not (Task6ExperimentStatus.PluginDeployed or Task6ExperimentStatus.LaunchObserved or Task6ExperimentStatus.ManualClosureRequired))
        {
            throw new PluginSmokeException("The Task-6 ownership record does not describe a deployed plugin that can be removed.");
        }

        _ = SmokeTestPathValidator.ValidateRoots(
            Value(args, "--repository-root"),
            Value(args, "--original-game"),
            experimentRoot);
        PluginDeploymentService.Remove(
            Value(args, "--clean-game"),
            OptionalValue(args, "--plugin-guid") ?? "dev.throneforge.m1.synthetic-smoke");
        Task6ExperimentStateService.SaveAtomic(experimentRoot, state with
        {
            Status = Task6ExperimentStatus.LoaderApplied,
            PluginRelativeRoot = null
        });
        stdout.WriteLine("synthetic-plugin-removed=true");
        return 0;
    }

    private static int ParseMarker(string[] args, TextWriter stdout)
    {
        var result = PluginSmokeMarkerParser.Parse(
            File.ReadAllText(Value(args, "--log-path")),
            Value(args, "--nonce"));
        stdout.WriteLine($"marker-valid={result.IsValid}");
        stdout.WriteLine($"marker-count={result.MarkerCount}");
        stdout.WriteLine($"lifecycle-marker={result.LifecycleMarkerDetected}");
        if (!result.IsValid)
        {
            stdout.WriteLine($"failure-category={result.FailureCategory}");
        }

        return result.IsValid ? 0 : 1;
    }

    private static int Inspect(string[] args, TextWriter stdout)
    {
        var metadata = PluginAssemblyMetadataInspector.Inspect(
            Value(args, "--assembly-path"),
            Value(args, "--relative-path"));
        stdout.WriteLine($"assembly-identity={metadata.AssemblyIdentity}");
        stdout.WriteLine($"target-framework={metadata.TargetFramework ?? "unknown"}");
        stdout.WriteLine($"managed={metadata.HasManagedMetadata}");
        stdout.WriteLine($"clr-header={metadata.ClrHeaderPresent}");
        stdout.WriteLine($"il-only={metadata.IlOnly}");
        stdout.WriteLine($"native-entry-point={metadata.NativeEntryPointPresent}");
        stdout.WriteLine($"managed-native-header={metadata.ManagedNativeHeaderPresent}");
        stdout.WriteLine($"pinvoke-count={metadata.PInvokeEntryCount}");
        stdout.WriteLine($"module-initializer={metadata.ModuleInitializerPresent}");
        stdout.WriteLine($"assembly-references={string.Join(',', metadata.AssemblyReferences)}");
        stdout.WriteLine($"sha256={metadata.Sha256.Value}");
        return 0;
    }

    private static int InspectLifecycleBinding(string[] args, TextWriter stdout)
    {
        var result = UnityLifecycleMetadataInspector.Inspect(Value(args, "--assembly-path"));
        stdout.WriteLine($"binding-id={result.BindingId}");
        stdout.WriteLine($"metadata-valid={result.IsValid}");
        stdout.WriteLine($"assembly-identity={result.AssemblyIdentity ?? "unknown"}");
        stdout.WriteLine($"source-type={result.SourceType ?? "unknown"}");
        stdout.WriteLine($"source-event={result.SourceEvent ?? "unknown"}");
        stdout.WriteLine($"handler-type={result.HandlerType ?? "unknown"}");
        if (!result.IsValid)
        {
            stdout.WriteLine($"failure-category={result.FailureCategory}");
        }

        return result.IsValid ? 0 : 1;
    }

    private static int Tfm(string[] args, TextWriter stdout)
    {
        var evidence = AssemblyPaths(args)
            .Select(path =>
            {
                var metadata = PluginAssemblyMetadataInspector.Inspect(path, Path.GetFileName(path));
                var fileName = Path.GetFileName(path);
                return new ManagedAssemblyCompatibilityEvidence(
                    fileName,
                    metadata.AssemblyIdentity,
                    NormalizeTargetFramework(metadata.TargetFramework),
                    metadata.HasManagedMetadata,
                    fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                        || metadata.TargetFramework?.StartsWith(".NETStandard,Version=v", StringComparison.OrdinalIgnoreCase) == true);
            })
            .ToArray();
        var assessment = PluginTargetFrameworkSelector.Select(evidence, Value(args, "--unity-version"));
        stdout.WriteLine($"recommendation={assessment.Recommendation}");
        stdout.WriteLine($"confidence={assessment.Confidence}");
        stdout.WriteLine($"basis={assessment.Basis}");
        return assessment.Recommendation == PluginTargetFramework.Inconclusive ? 1 : 0;
    }

    private static int Launch(string[] args, TextWriter stdout)
    {
        var result = ThroneForge.LoaderSmokeTest.LaunchObservationService.Observe(
            Value(args, "--executable"),
            Value(args, "--clean-game"),
            Value(args, "--experiment-root"),
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(10),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["THRONEFORGE_SMOKE_NONCE"] = Value(args, "--nonce")
            });
        stdout.WriteLine($"started={result.Started}");
        stdout.WriteLine($"stable-initialized={result.StableInitialized}");
        stdout.WriteLine($"exited={result.Exited}");
        stdout.WriteLine($"gracefully-closed={result.Exited && !result.RequiresManualClosure}");
        stdout.WriteLine($"manual-closure-required={result.RequiresManualClosure}");
        stdout.WriteLine($"failure-category={result.FailureCategory}");
        return result.Started && result.Exited && !result.RequiresManualClosure ? 0 : 1;
    }

    private static int VerifyLog(string[] args, TextWriter stdout)
    {
        var summary = PluginSmokeLogParser.Parse(
            File.ReadAllText(Value(args, "--log-path")),
            Value(args, "--nonce"),
            Value(args, "--api-identity"),
            Value(args, "--contracts-identity"));
        stdout.WriteLine($"bepinex-version={summary.Loader.BepInExVersion ?? "unknown"}");
        stdout.WriteLine($"preloader={summary.Loader.PreloaderInitialized}");
        stdout.WriteLine($"chainloader={summary.Loader.ChainloaderInitialized}");
        stdout.WriteLine($"plugins={summary.Loader.PluginsDiscovered}");
        stdout.WriteLine($"warnings={summary.Loader.WarningCount}");
        stdout.WriteLine($"errors={summary.Loader.ErrorCount}");
        stdout.WriteLine($"fatal-errors={summary.Loader.FatalErrorCount}");
        stdout.WriteLine($"marker={summary.Marker.IsValid}");
        stdout.WriteLine($"marker-count={summary.Marker.MarkerCount}");
        stdout.WriteLine($"lifecycle-marker={summary.Marker.LifecycleMarkerDetected}");
        stdout.WriteLine($"runtime-api-identity={summary.Marker.Marker?.ApiIdentity ?? "unknown"}");
        stdout.WriteLine($"runtime-contracts-identity={summary.Marker.Marker?.ContractsIdentity ?? "unknown"}");
        stdout.WriteLine($"smoke-criteria={summary.MeetsCriteria}");
        return summary.MeetsCriteria ? 0 : 1;
    }

    private static int VerifyLifecycleLog(string[] args, TextWriter stdout)
    {
        var stable = LifecycleLogStabilityObserver.Observe(
            Value(args, "--log-path"),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(100));
        if (!stable.IsStable || stable.Text is null)
        {
            stdout.WriteLine("log-stable=false");
            stdout.WriteLine($"failure-category={stable.FailureCategory}");
            return 1;
        }

        var loader = ThroneForge.LoaderSmokeTest.LoaderLogParser.Parse(stable.Text);
        var marker = LifecycleMarkerParser.Parse(
            stable.Text,
            Value(args, "--nonce"),
            Value(args, "--api-identity"),
            Value(args, "--contracts-identity"));
        var firstMarker = marker.Markers.Count == 0 ? null : marker.Markers[0];
        var meetsCriteria = string.Equals(loader.BepInExVersion, "5.4.23.5", StringComparison.Ordinal)
            && loader.PreloaderInitialized
            && loader.ChainloaderInitialized
            && loader.PluginsDiscovered == 1
            && loader.ErrorCount == 0
            && loader.FatalErrorCount == 0
            && marker.IsValid;

        stdout.WriteLine("log-stable=true");
        stdout.WriteLine($"bepinex-version={loader.BepInExVersion ?? "unknown"}");
        stdout.WriteLine($"preloader={loader.PreloaderInitialized}");
        stdout.WriteLine($"chainloader={loader.ChainloaderInitialized}");
        stdout.WriteLine($"plugins={loader.PluginsDiscovered}");
        stdout.WriteLine($"warnings={loader.WarningCount}");
        stdout.WriteLine($"errors={loader.ErrorCount}");
        stdout.WriteLine($"fatal-errors={loader.FatalErrorCount}");
        stdout.WriteLine($"marker={marker.IsValid}");
        stdout.WriteLine($"initialization-count={marker.InitializationCount}");
        stdout.WriteLine($"quitting-count={marker.QuittingCount}");
        stdout.WriteLine($"shutdown-count={marker.ShutdownCount}");
        stdout.WriteLine($"marker-sequence={string.Join(',', marker.Markers.Select(item => item.Sequence))}");
        stdout.WriteLine($"runtime-api-identity={firstMarker?.ApiIdentity ?? "unknown"}");
        stdout.WriteLine($"runtime-contracts-identity={firstMarker?.ContractsIdentity ?? "unknown"}");
        stdout.WriteLine($"lifecycle-criteria={meetsCriteria}");
        if (!marker.IsValid)
        {
            stdout.WriteLine($"failure-category={marker.FailureCategory}");
        }

        return meetsCriteria ? 0 : 1;
    }

    private static int Manifest(string[] args, TextWriter stdout)
    {
        var manifest = ThroneForge.LoaderSmokeTest.InstallationCopyService.CaptureManifest(Value(args, "--root"));
        stdout.WriteLine($"manifest-identity={ThroneForge.LoaderSmokeTest.InstallationCopyService.ComputeManifestIdentity(manifest)}");
        stdout.WriteLine($"file-count={manifest.Files.Count}");
        stdout.WriteLine($"directory-count={(manifest.Directories ?? []).Count}");
        return 0;
    }

    private static int Ownership(string[] args, TextWriter stdout)
    {
        var experimentRoot = Value(args, "--experiment-root");
        var fingerprint = Value(args, "--expected-fingerprint");
        var status = Enum.Parse<Task6ExperimentStatus>(Value(args, "--status"), ignoreCase: false);
        var statePath = Task6ExperimentStateService.GetStatePath(experimentRoot);
        var current = File.Exists(statePath)
            ? Task6ExperimentStateService.LoadAndValidate(experimentRoot, fingerprint)
            : Task6ExperimentStateService.CreatePrepared(experimentRoot, fingerprint, Value(args, "--repository-commit"));
        var updated = current with
        {
            Status = status,
            PackageSha256 = OptionalValue(args, "--package-sha256") ?? current.PackageSha256,
            AdmissionBindingDigest = OptionalValue(args, "--binding-digest") ?? current.AdmissionBindingDigest,
            PluginRelativeRoot = OptionalValue(args, "--plugin-root") ?? current.PluginRelativeRoot,
            LoaderTransactionStatus = OptionalValue(args, "--loader-status") ?? current.LoaderTransactionStatus
        };
        Task6ExperimentStateService.SaveAtomic(experimentRoot, updated);
        stdout.WriteLine($"ownership-status={updated.Status}");
        stdout.WriteLine($"experiment-id={updated.ExperimentId}");
        return 0;
    }

    private static int Recovery(string[] args, TextWriter stdout)
    {
        var experimentRoot = Value(args, "--experiment-root");
        var fingerprint = Value(args, "--expected-fingerprint");
        var ownership = Task6ExperimentStateService.LoadAndValidate(experimentRoot, fingerprint);
        var recovery = new Task6RecoveryState(
            Task6ExperimentStateService.SchemaVersion,
            Task6ExperimentStateService.TaskVersion,
            fingerprint.ToLowerInvariant(),
            ownership.ExperimentId,
            OptionalValue(args, "--package-sha256") ?? ownership.PackageSha256,
            OptionalValue(args, "--binding-digest") ?? ownership.AdmissionBindingDigest,
            OptionalValue(args, "--plugin-root") ?? Task6ExperimentStateService.SyntheticPluginRelativeRoot,
            OptionalValue(args, "--loader-status") ?? "RollbackRequired",
            "ManualClosureRequired");
        Task6ExperimentStateService.SaveRecoveryAtomic(experimentRoot, recovery);
        var updated = ownership with { Status = Task6ExperimentStatus.ManualClosureRequired };
        Task6ExperimentStateService.SaveAtomic(experimentRoot, updated);
        stdout.WriteLine("recovery-marker=persisted");
        stdout.WriteLine($"experiment-id={ownership.ExperimentId}");
        return 0;
    }

    private static int CleanupOwned(string[] args, TextWriter stdout)
    {
        var experimentRoot = Value(args, "--experiment-root");
        var expectedFingerprint = Value(args, "--expected-fingerprint");
        var roots = SmokeTestPathValidator.ValidateRoots(
            Value(args, "--repository-root"),
            Value(args, "--original-game"),
            experimentRoot);
        var state = Task6ExperimentStateService.LoadAndValidate(experimentRoot, expectedFingerprint);
        if (state.Status is not (Task6ExperimentStatus.RolledBack or Task6ExperimentStatus.Completed or Task6ExperimentStatus.Failed))
        {
            throw new PluginSmokeException("Cleanup requires an owned Task-6 experiment that has already been rolled back or failed closed.");
        }

        SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(roots.ExperimentRoot);
        Directory.Delete(roots.ExperimentRoot, recursive: true);
        stdout.WriteLine("owned-cleanup=completed");
        return 0;
    }

    private static int LifecycleStage(string[] args, TextWriter stdout)
    {
        var experimentRoot = Value(args, "--experiment-root");
        var experimentId = Value(args, "--experiment-id");
        var fingerprint = Value(args, "--expected-fingerprint");
        var currentStage = Enum.Parse<LifecycleExperimentStage>(Value(args, "--current-stage"), ignoreCase: false);
        var lastCompleted = OptionalValue(args, "--last-completed-stage") is { } rawLast
            ? Enum.Parse<LifecycleExperimentStage>(rawLast, ignoreCase: false)
            : (LifecycleExperimentStage?)null;
        var resultCategory = OptionalValue(args, "--result-category") ?? LifecycleExperimentFailureCategories.StageCompleted;
        var statePath = LifecycleExperimentStageStateService.GetStatePath(experimentRoot);
        if (File.Exists(statePath))
        {
            _ = LifecycleExperimentStageStateService.LoadAndValidate(experimentRoot, experimentId, fingerprint);
        }

        var state = LifecycleExperimentStageStateService.Advance(
            experimentRoot,
            experimentId,
            fingerprint,
            currentStage,
            lastCompleted,
            resultCategory,
            OptionalValue(args, "--loader-status"),
            OptionalValue(args, "--package-sha256"),
            OptionalValue(args, "--binding-digest"));
        stdout.WriteLine($"current-stage={state.CurrentStage}");
        stdout.WriteLine($"last-completed-stage={state.LastCompletedStage?.ToString() ?? "none"}");
        stdout.WriteLine($"result-category={state.ResultCategory}");
        return 0;
    }

    private static List<string> AssemblyPaths(string[] args)
    {
        var result = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].Equals("--assembly-path", StringComparison.Ordinal) && index + 1 < args.Length)
            {
                result.Add(args[++index]);
            }
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("At least one --assembly-path value is required.");
        }

        return result;
    }

    private static bool IsLifecycle(string[] args)
        => string.Equals(OptionalValue(args, "--package-kind"), "lifecycle", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("lifecycle-package", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeTargetFramework(string? targetFramework)
    {
        if (targetFramework is null)
        {
            return null;
        }

        if (targetFramework.Contains(".NETStandard,Version=v2.0", StringComparison.OrdinalIgnoreCase))
        {
            return "netstandard2.0";
        }

        if (targetFramework.Contains(".NETStandard,Version=v2.1", StringComparison.OrdinalIgnoreCase))
        {
            return "netstandard2.1";
        }

        return targetFramework;
    }

    private static string Value(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return args[index + 1];
    }

    private static string? OptionalValue(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

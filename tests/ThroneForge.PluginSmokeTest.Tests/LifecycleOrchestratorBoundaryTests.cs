using ThroneForge.PluginSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LifecycleOrchestratorBoundaryTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";
    private const string RepositoryCommit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void ReportWriterDerivesClaimsFromResultAndRedactsAbsoluteValues()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "throneforge-task7-report", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repositoryRoot, "docs", "discovery"));
        try
        {
            var result = new LifecycleExperimentResult(
                "Failed",
                LifecycleExperimentStage.OriginalPreflight,
                LifecycleExperimentStage.OriginalPreflight,
                null,
                LifecycleExperimentFailureCategories.OriginalPreflightFailed,
                true,
                LifecycleExperimentStage.OriginalPreflight,
                LifecycleExperimentFailureCategories.OriginalPreflightFailed,
                null,
                "C:\\private\\game.exe",
                null,
                "C:\\private\\UnityEngine.CoreModule.dll");

            var path = new LifecycleExperimentReportWriter(repositoryRoot, Fingerprint).Write(result);
            var text = File.ReadAllText(path);

            Assert.DoesNotContain("C:\\private", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OriginalPreflight", text, StringComparison.Ordinal);
            Assert.Contains("original-preflight-failed", text, StringComparison.Ordinal);
            Assert.Contains("not-observed", text, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LifecycleCliHasOneRealOrchestratorOperation()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = PluginSmokeCli.Run(["run-lifecycle-experiment"], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("--repository-root", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Stage", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleCliExecutesTheRealOrchestratorAndWritesItsResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-cli", Guid.NewGuid().ToString("N"));
        var repositoryRoot = Path.Combine(root, "repository");
        var gameRoot = Path.Combine(root, "game");
        var experimentRoot = Path.Combine(root, "experiment");
        var packageRoot = Path.Combine(experimentRoot, "package");
        Directory.CreateDirectory(Path.Combine(repositoryRoot, "docs", "discovery"));
        Directory.CreateDirectory(gameRoot);
        Directory.CreateDirectory(experimentRoot);
        Directory.CreateDirectory(packageRoot);
        var unityPath = Path.Combine(gameRoot, "Thronefall_Data", "Managed", "UnityEngine.CoreModule.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(unityPath)!);
        File.WriteAllBytes(unityPath, [0]);
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = PluginSmokeCli.Run(
            [
                "run-lifecycle-experiment",
                "--repository-root", repositoryRoot,
                "--original-game", gameRoot,
                "--experiment-root", experimentRoot,
                "--expected-fingerprint", Fingerprint,
                "--bepinex-archive", Path.Combine(root, "loader.zip"),
                "--official-digest", new string('c', 64),
                "--package-root", packageRoot,
                "--manifest-path", Path.Combine(experimentRoot, "package-manifest.json"),
                "--unity-assembly", unityPath,
                "--executable-relative-path", "Thronefall.exe",
                "--repository-baseline-commit", RepositoryCommit
            ], stdout, stderr);

            Assert.Equal(1, exitCode);
            Assert.Contains("result=Failed", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("report=", stdout.ToString(), StringComparison.Ordinal);
            Assert.Empty(stderr.ToString());
            Assert.False(File.Exists(Path.Combine(experimentRoot, "evidence", "lifecycle-stage-state.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void OwnershipFailureDoesNotRewriteAnExistingTask6Owner()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-owner-boundary", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var owner = Task6ExperimentStateService.CreatePrepared(root, Fingerprint, RepositoryCommit);
            Task6ExperimentStateService.SaveAtomic(root, owner);

            var result = new LifecycleExperimentOrchestrator(
                root,
                Guid.NewGuid().ToString("N"),
                Fingerprint,
                new OwnershipRejectingOperations()).Run();

            Assert.Equal("Failed", result.OverallResult);
            Assert.Equal(LifecycleExperimentFailureCategories.OwnershipStateInvalid, result.PrimaryFailureCategory);
            Assert.Equal(Task6ExperimentStatus.Prepared, Task6ExperimentStateService.LoadAndValidate(root, Fingerprint).Status);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PowerShellWrapperContainsNoSecondStageMachine()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var script = File.ReadAllText(Path.Combine(repositoryRoot, "tools", "plugin-smoke-test", "Invoke-ThroneForgeLifecycleBindingSmokeTest.ps1"));

        Assert.Contains("run-lifecycle-experiment", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Stage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Complete-Stage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Fail-CurrentStage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("admit-and-deploy", script, StringComparison.Ordinal);
        Assert.DoesNotContain("lifecycle-binding.md", script, StringComparison.Ordinal);
        Assert.Contains("git -C", script, StringComparison.Ordinal);
        Assert.Contains("rollback-lifecycle-experiment", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$RepositoryBaselineCommit", script, StringComparison.Ordinal);
    }

    private sealed class OwnershipRejectingOperations : ILifecycleExperimentOperations
    {
        public LifecycleStageEvidence EnsureOwnership(LifecycleExperimentContext context)
            => new(false, LifecycleExperimentFailureCategories.OwnershipStateInvalid);

        public OriginalPreflightEvidence OriginalPreflight(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LifecycleStageEvidence DisposablePrepare(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LifecycleStageEvidence BaselineLaunch(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LifecycleStageEvidence LoaderInstall(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LifecycleStageEvidence LoaderLaunch(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LoaderVerificationEvidence LoaderVerify(LifecycleExperimentContext context) => throw new NotSupportedException();
        public UnityMetadataEvidence UnityMetadataPreflight(LifecycleExperimentContext context) => throw new NotSupportedException();
        public PackageEvidence PackageBuild(LifecycleExperimentContext context) => throw new NotSupportedException();
        public PackageEvidence PackageCapture(LifecycleExperimentContext context) => throw new NotSupportedException();
        public DeploymentEvidence AdmitAndDeploy(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LifecycleStageEvidence LifecycleLaunch(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LogStabilityEvidence LogStability(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LifecycleVerificationEvidence LifecycleVerification(LifecycleExperimentContext context) => throw new NotSupportedException();
        public CleanupEvidence PluginRemoval(LifecycleExperimentContext context) => throw new NotSupportedException();
        public CleanupEvidence LoaderRollback(LifecycleExperimentContext context) => throw new NotSupportedException();
        public PostcheckEvidence DisposablePostcheck(LifecycleExperimentContext context) => throw new NotSupportedException();
        public PostcheckEvidence OriginalPostcheck(LifecycleExperimentContext context) => throw new NotSupportedException();
        public RecoveryEvidence PersistManualClosureRecovery(LifecycleExperimentContext context) => throw new NotSupportedException();
        public LifecycleStageEvidence FinalizeFailure(LifecycleExperimentContext context)
            => throw new InvalidOperationException("FinalizeFailure must not run before ownership succeeds.");
    }
}

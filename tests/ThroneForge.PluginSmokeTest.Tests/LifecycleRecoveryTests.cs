using ThroneForge.LoaderSmokeTest;
using ThroneForge.PluginSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LifecycleRecoveryTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";
    private const string RepositoryCommit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void FailedOwnershipWithoutTransactionReturnsTransactionMissing()
    {
        var root = CreateRoot();
        try
        {
            var roots = CreateRoots(root);
            var manifest = new CopyManifest([], []);
            Directory.CreateDirectory(roots.OriginalGameRoot);
            Directory.CreateDirectory(roots.CleanGameRoot);
            Directory.CreateDirectory(roots.RepositoryRoot);
            var baseline = new DisposableProfileBaseline(
                DisposableProfileBaselineService.SchemaVersion,
                DisposableProfileBaselineService.TaskVersion,
                Fingerprint,
                manifest,
                manifest);
            DisposableProfileBaselineService.Save(LoaderSmokeTestStatePaths.GetBaselinePath(roots), baseline);
            Task6ExperimentStateService.SaveAtomic(
                roots.ExperimentRoot,
                new Task6ExperimentState(
                    Task6ExperimentStateService.SchemaVersion,
                    Task6ExperimentStateService.TaskVersion,
                    Fingerprint,
                    Guid.NewGuid().ToString("N"),
                    RepositoryCommit,
                    Task6ExperimentStateService.CleanGameRelativePath,
                    Task6ExperimentStatus.Failed,
                    LoaderTransactionStatus: LoaderTransactionStatus.RollbackRequired.ToString()));

            var result = LifecycleExperimentRecoveryService.Rollback(new LifecycleExperimentRecoveryOptions(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                Fingerprint,
                Path.Combine(root, "BepInEx_win_x64_5.4.23.5.zip"),
                new string('a', 64)));

            Assert.Equal("Failed", result.OverallResult);
            Assert.Equal("recovery-transaction-missing", result.FailureCategory);
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
    public void PluginDeployedRecoveryRemovesOwnedPluginBeforeRuntimeDriftCheck()
    {
        var root = CreateRoot();
        try
        {
            var roots = CreateRoots(root);
            Directory.CreateDirectory(roots.RepositoryRoot);
            Directory.CreateDirectory(roots.OriginalGameRoot);
            Directory.CreateDirectory(roots.CleanGameRoot);
            File.WriteAllText(Path.Combine(roots.OriginalGameRoot, "game.dat"), "baseline");
            File.WriteAllText(Path.Combine(roots.CleanGameRoot, "game.dat"), "baseline");

            var originalManifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
            var disposableManifest = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
            DisposableProfileBaselineService.Save(
                LoaderSmokeTestStatePaths.GetBaselinePath(roots),
                new DisposableProfileBaseline(
                    DisposableProfileBaselineService.SchemaVersion,
                    DisposableProfileBaselineService.TaskVersion,
                    Fingerprint,
                    originalManifest,
                    disposableManifest));

            var loaderFile = Path.Combine(roots.CleanGameRoot, "BepInEx", "core.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(loaderFile)!);
            File.WriteAllText(loaderFile, "loader");
            Directory.CreateDirectory(Path.Combine(roots.CleanGameRoot, "BepInEx", "plugins"));
            var loaderOnlyManifest = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
            var loaderHash = loaderOnlyManifest.Files.Single(item => item.RelativePath == "BepInEx/core.dll").Sha256;
            var pluginRoot = Path.Combine(roots.CleanGameRoot, "BepInEx", "plugins", "dev.throneforge.m1.lifecycle-smoke");
            Directory.CreateDirectory(pluginRoot);
            foreach (var fileName in new[]
            {
                "ThroneForge.M1.LifecycleSmoke.dll",
                "ThroneForge.API.dll",
                "ThroneForge.Contracts.dll"
            })
            {
                File.WriteAllText(Path.Combine(pluginRoot, fileName), fileName);
            }

            var transaction = new LoaderTransactionState(
                LoaderTransactionStateService.SchemaVersion,
                LoaderTransactionStateService.TaskVersion,
                Fingerprint,
                InstallationCopyService.ComputeManifestIdentity(disposableManifest),
                "BepInEx_win_x64_5.4.23.5.zip",
                new string('a', 64),
                LoaderTransactionStatus.RollbackRequired,
                loaderOnlyManifest,
                [new TransactionEntry("BepInEx/core.dll", TransactionChangeKind.NewFile, null, loaderHash, null)],
                [],
                [],
                null);
            LoaderTransactionStateService.SaveAtomic(
                LoaderSmokeTestStatePaths.GetTransactionStatePath(roots),
                transaction);
            Task6ExperimentStateService.SaveAtomic(
                roots.ExperimentRoot,
                new Task6ExperimentState(
                    Task6ExperimentStateService.SchemaVersion,
                    Task6ExperimentStateService.TaskVersion,
                    Fingerprint,
                    Guid.NewGuid().ToString("N"),
                    RepositoryCommit,
                    Task6ExperimentStateService.CleanGameRelativePath,
                    Task6ExperimentStatus.Failed,
                    PluginRelativeRoot: Task6ExperimentStateService.LifecyclePluginRelativeRoot,
                    LoaderTransactionStatus: LoaderTransactionStatus.RollbackRequired.ToString(),
                    LoaderOnlyManifest: loaderOnlyManifest));

            var result = LifecycleExperimentRecoveryService.Rollback(new LifecycleExperimentRecoveryOptions(
                roots.RepositoryRoot,
                roots.OriginalGameRoot,
                roots.ExperimentRoot,
                Fingerprint,
                Path.Combine(root, "BepInEx_win_x64_5.4.23.5.zip"),
                new string('a', 64)));

            Assert.False(Directory.Exists(pluginRoot));
            Assert.Equal(CleanupOperationStatus.Passed, result.PluginRemovalStatus);
            Assert.NotEqual(LifecycleExperimentFailureCategories.RecoveryRuntimeDrift, result.FailureCategory);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-recovery", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static SmokeTestRoots CreateRoots(string root)
        => new(
            Path.Combine(root, "repository"),
            Path.Combine(root, "original"),
            Path.Combine(root, "experiment"),
            Path.Combine(root, "experiment", "clean-game"),
            Path.Combine(root, "experiment", "downloads"),
            Path.Combine(root, "experiment", "extracted-loader"),
            Path.Combine(root, "experiment", "evidence"),
            Path.Combine(root, "experiment", "manifests"),
            Path.Combine(root, "experiment", "manifests", "backup"));
}

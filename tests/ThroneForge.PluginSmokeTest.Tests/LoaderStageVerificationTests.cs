using System.Security.Cryptography;
using System.Text;
using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LoaderStageVerificationTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";

    [Fact]
    public void VerificationUsesCanonicalExperimentManifestAndSavedDisposableBaseline()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-loader-state", Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repo");
        var original = Path.Combine(root, "original");
        var experiment = Path.Combine(root, "experiment");
        try
        {
            Directory.CreateDirectory(repository);
            Directory.CreateDirectory(original);
            Directory.CreateDirectory(experiment);
            File.WriteAllText(Path.Combine(original, "game.dat"), "game");
            var roots = SmokeTestPathValidator.ValidateRoots(repository, original, experiment);
            var ownership = Task6ExperimentStateService.CreatePrepared(experiment, Fingerprint, new string('c', 40));
            Task6ExperimentStateService.SaveAtomic(experiment, ownership);
            Directory.CreateDirectory(roots.CleanGameRoot);
            File.Copy(Path.Combine(original, "game.dat"), Path.Combine(roots.CleanGameRoot, "game.dat"));

            var originalManifest = InstallationCopyService.CaptureManifest(original);
            var disposableManifest = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
            var baseline = new DisposableProfileBaseline(
                DisposableProfileBaselineService.SchemaVersion,
                DisposableProfileBaselineService.TaskVersion,
                Fingerprint,
                originalManifest,
                disposableManifest);
            DisposableProfileBaselineService.Save(LoaderSmokeTestStatePaths.GetBaselinePath(roots), baseline);

            var loaderPath = Path.Combine(roots.CleanGameRoot, "BepInEx", "core", "BepInEx.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(loaderPath)!);
            File.WriteAllText(loaderPath, "loader");
            var applied = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
            var loaderHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("loader"))).ToLowerInvariant();
            var transaction = new LoaderTransactionState(
                LoaderTransactionStateService.SchemaVersion,
                LoaderTransactionStateService.TaskVersion,
                Fingerprint,
                InstallationCopyService.ComputeManifestIdentity(disposableManifest),
                "BepInEx_win_x64_5.4.23.5.zip",
                new string('b', 64),
                LoaderTransactionStatus.Applied,
                applied,
                [new TransactionEntry("BepInEx/core/BepInEx.dll", TransactionChangeKind.NewFile, null, loaderHash, null)],
                []);
            LoaderTransactionStateService.SaveAtomic(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots), transaction);
            ownership = ownership with
            {
                Status = Task6ExperimentStatus.LoaderApplied,
                LoaderTransactionStatus = LoaderTransactionStatus.Applied.ToString()
            };
            Task6ExperimentStateService.SaveAtomic(experiment, ownership);

            var evidence = LoaderStageVerificationService.Verify(
                repository,
                original,
                experiment,
                Fingerprint,
                LoaderTransactionStatus.Applied);

            Assert.Equal("Applied", evidence.LoaderStatus);
            Assert.True(evidence.TransactionBaselineMatched);
            Assert.True(evidence.AppliedProfileMatched);
            Assert.Equal(
                InstallationCopyService.ComputeManifestIdentity(disposableManifest),
                evidence.BaselineManifestIdentity);
            Assert.True(File.Exists(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots)));
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
    public void LegacyCleanGameTransactionPathIsNotAccepted()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-loader-state", Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repo");
        var original = Path.Combine(root, "original");
        var experiment = Path.Combine(root, "experiment");
        try
        {
            Directory.CreateDirectory(repository);
            Directory.CreateDirectory(original);
            Directory.CreateDirectory(experiment);
            File.WriteAllText(Path.Combine(original, "game.dat"), "game");
            var roots = SmokeTestPathValidator.ValidateRoots(repository, original, experiment);
            Directory.CreateDirectory(roots.CleanGameRoot);
            File.Copy(Path.Combine(original, "game.dat"), Path.Combine(roots.CleanGameRoot, "game.dat"));
            Directory.CreateDirectory(Path.Combine(roots.CleanGameRoot, "manifests"));
            File.WriteAllText(Path.Combine(roots.CleanGameRoot, "manifests", "transaction-state.json"), "{}");

            Assert.False(File.Exists(LoaderSmokeTestStatePaths.GetTransactionStatePath(roots)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

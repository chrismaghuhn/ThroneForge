using ThroneForge.PluginSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LifecycleProductionStateTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";
    private const string RepositoryCommit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void ProductionOperationsCreateOwnedTask6StateBeforePreparation()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-production-state", Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repository");
        var original = Path.Combine(root, "original");
        var experiment = Path.Combine(root, "experiment");
        Directory.CreateDirectory(repository);
        Directory.CreateDirectory(original);
        var unity = Path.Combine(original, "Thronefall_Data", "Managed", "UnityEngine.CoreModule.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(unity)!);
        File.WriteAllBytes(unity, [0]);
        var packageRoot = Path.Combine(experiment, "package");
        var options = new LifecycleExperimentProductionOptions(
            repository,
            original,
            experiment,
            Fingerprint,
            Path.Combine(root, "BepInEx.zip"),
            new string('a', 64),
            packageRoot,
            Path.Combine(experiment, "manifests", "package.json"),
            unity,
            "Thronefall.exe",
            "nonce",
            RepositoryBaselineCommit: RepositoryCommit);

        try
        {
            var operations = new LifecycleExperimentProductionOperations(options);
            var context = new LifecycleExperimentContext(experiment, Guid.NewGuid().ToString("N"), Fingerprint, RepositoryCommit);
            var evidence = operations.EnsureOwnership(context);

            Assert.True(evidence.Succeeded, evidence.FailureCategory);
            var state = Task6ExperimentStateService.LoadAndValidate(experiment, Fingerprint, RepositoryCommit);
            Assert.Equal(Task6ExperimentStatus.Prepared, state.Status);
            Assert.Equal(context.ExperimentId, state.ExperimentId);

            Assert.True(operations.FinalizeFailure(context).Succeeded);
            Assert.Equal(Task6ExperimentStatus.Failed, Task6ExperimentStateService.LoadAndValidate(experiment, Fingerprint).Status);
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
    public void RepositoryBaselineCommitMustBeAnExactSha1()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-commit-state", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<PluginSmokeException>(() => Task6ExperimentStateService.CreatePrepared(root, Fingerprint, "not-a-commit"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

}

using ThroneForge.PluginSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LifecycleProductionStateTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";

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
            RepositoryBaselineCommit: "test-baseline");

        try
        {
            var operations = new LifecycleExperimentProductionOperations(options);
            var context = new LifecycleExperimentContext(experiment, Guid.NewGuid().ToString("N"), Fingerprint, "test-baseline");
            var evidence = operations.EnsureOwnership(context);

            Assert.True(evidence.Succeeded, evidence.FailureCategory);
            var state = Task6ExperimentStateService.LoadAndValidate(experiment, Fingerprint, "test-baseline");
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

}

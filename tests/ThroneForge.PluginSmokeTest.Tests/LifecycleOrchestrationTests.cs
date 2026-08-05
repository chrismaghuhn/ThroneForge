using ThroneForge.PluginSmokeTest;
using Xunit;

namespace ThroneForge.PluginSmokeTest.Tests;

public sealed class LifecycleOrchestrationTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";

    [Fact]
    public void RequiredStagesUseTruthfulAdmitAndDeployPhase()
    {
        Assert.Contains(LifecycleExperimentStage.AdmitAndDeploy, LifecycleExperimentOrchestrator.RequiredStages);
        Assert.DoesNotContain(LifecycleExperimentStage.Admission, LifecycleExperimentOrchestrator.RequiredStages);
        Assert.DoesNotContain(LifecycleExperimentStage.Deployment, LifecycleExperimentOrchestrator.RequiredStages);
        Assert.Equal(LifecycleExperimentStage.OriginalPostcheck, LifecycleExperimentOrchestrator.RequiredStages[^1]);
    }

    [Fact]
    public void AllStagesAdvanceToCompletedAndPersistState()
    {
        var root = CreateRoot();
        try
        {
            var result = new LifecycleExperimentOrchestrator(
                root,
                Guid.NewGuid().ToString("N"),
                Fingerprint,
                LifecycleExperimentHooks.All(_ => new LifecycleStageOperationResult(true)))
                .Run();

            Assert.Equal("Passed", result.OverallResult);
            Assert.Equal(LifecycleExperimentStage.Completed, result.CurrentStage);
            Assert.Equal(LifecycleExperimentStage.OriginalPostcheck, result.LastCompletedStage);
            Assert.True(result.StageStatePersisted);
            Assert.Equal(LifecycleExperimentFailureCategories.StageCompleted, result.StableCategory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoaderInstallFailureStopsBeforeLaterStages()
    {
        var root = CreateRoot();
        var called = new List<LifecycleExperimentStage>();
        try
        {
            var operations = LifecycleExperimentOrchestrator.RequiredStages.ToDictionary(
                stage => stage,
                stage => new Func<LifecycleExperimentStageContext, LifecycleStageOperationResult>(_ =>
                {
                    called.Add(stage);
                    return stage == LifecycleExperimentStage.LoaderInstall
                        ? new(false, LifecycleExperimentFailureCategories.LoaderInstallFailed)
                        : new(true);
                }));

            var result = new LifecycleExperimentOrchestrator(
                root,
                Guid.NewGuid().ToString("N"),
                Fingerprint,
                LifecycleExperimentHooks.Create(operations))
                .Run();

            Assert.Equal("Failed", result.OverallResult);
            Assert.Equal(LifecycleExperimentStage.LoaderInstall, result.FailedStage);
            Assert.Equal(LifecycleExperimentFailureCategories.LoaderInstallFailed, result.StableCategory);
            Assert.DoesNotContain(LifecycleExperimentStage.LoaderLaunch, called);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LogAndMarkerFailuresRemainDistinct()
    {
        Assert.NotEqual(LifecycleExperimentFailureCategories.LogNotStable, LifecycleExperimentFailureCategories.LifecycleMarkerInvalid);
        Assert.NotEqual(LifecycleExperimentFailureCategories.LogNotReadable, LifecycleExperimentFailureCategories.LifecycleMarkerMissing);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "throneforge-task7-orchestrator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}

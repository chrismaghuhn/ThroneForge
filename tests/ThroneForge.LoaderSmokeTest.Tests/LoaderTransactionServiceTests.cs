using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class LoaderTransactionServiceTests
{
    [Fact]
    public void ApplyAndRollbackRestoreOverwrittenAndNewFiles()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "existing.txt"), "original");
        fixture.WriteArchive(("new/", ""), ("new/file.txt", "new"), ("existing.txt", "replacement"));

        var extracted = ArchiveSafetyService.Extract(fixture.ArchivePath, roots.ExtractedLoaderRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);
        LoaderTransactionService.Apply(roots, plan, extracted);

        Assert.Equal("replacement", File.ReadAllText(Path.Combine(roots.CleanGameRoot, "existing.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(roots.CleanGameRoot, "new", "file.txt")));
        Assert.True(LoaderTransactionService.Verify(roots, plan, extracted));

        LoaderTransactionService.Rollback(roots, plan);

        Assert.Equal("original", File.ReadAllText(Path.Combine(roots.CleanGameRoot, "existing.txt")));
        Assert.False(File.Exists(Path.Combine(roots.CleanGameRoot, "new", "file.txt")));
    }

    [Fact]
    public void FailedApplyRollsBackAllMutations()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "existing.txt"), "original");
        fixture.WriteArchive(("new/", ""), ("new/file.txt", "new"), ("existing.txt", "replacement"));

        var extracted = ArchiveSafetyService.Extract(fixture.ArchivePath, roots.ExtractedLoaderRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);

        Assert.Throws<SmokeTestException>(() => LoaderTransactionService.Apply(roots, plan, extracted, failAfterEntries: 1));
        Assert.Equal("original", File.ReadAllText(Path.Combine(roots.CleanGameRoot, "existing.txt")));
        Assert.False(File.Exists(Path.Combine(roots.CleanGameRoot, "new", "file.txt")));
    }
}

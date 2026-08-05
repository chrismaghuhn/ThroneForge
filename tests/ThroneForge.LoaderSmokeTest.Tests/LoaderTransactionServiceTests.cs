using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class LoaderTransactionServiceTests
{
    [Fact]
    public void PersistedFailedAndRolledBackStateCannotBeUsedForLaunch()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        var baseline = new CopyManifest([new FileManifestEntry("game.txt", 4, "hash")], []);
        var state = CreateState(
            roots,
            baseline,
            LoaderTransactionStatus.FailedAndRolledBack,
            [new TransactionEntry("BepInEx/core.dll", TransactionChangeKind.NewFile, null, new string('a', 64), null)]);
        var path = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        LoaderTransactionStateService.SaveAtomic(path, state);

        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.LoadAndValidate(
            path,
            roots,
            new string('a', 64),
            baseline,
            [LoaderTransactionStatus.Applied, LoaderTransactionStatus.LaunchObserved]));
    }

    [Fact]
    public void EmptyPersistedTransactionCannotBeUsedForVerification()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        var baseline = new CopyManifest([new FileManifestEntry("game.txt", 4, new string('c', 64))], []);
        var state = CreateState(roots, baseline, LoaderTransactionStatus.LaunchObserved, []);
        state = state with
        {
            LaunchEvidence = new LoaderBootstrapEvidence("5.4.23.5", true, true, 0, 0, 0, 0)
        };

        var path = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        LoaderTransactionStateService.SaveAtomic(path, state);

        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.LoadAndValidate(
            path,
            roots,
            state.ExpectedFingerprint,
            baseline,
            [LoaderTransactionStatus.LaunchObserved]));
    }

    [Fact]
    public void ValidAppliedStateAndProfileAreAccepted()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "game.txt"), "game");
        fixture.WriteArchive(("BepInEx/core.dll", "loader"));
        var extracted = ArchiveSafetyService.Extract(fixture.ArchivePath, roots.ExtractedLoaderRoot);
        var baseline = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);
        LoaderTransactionService.Apply(roots, plan, extracted);
        var expected = LoaderTransactionService.BuildExpectedAppliedManifest(baseline, extracted);
        var state = CreateState(roots, baseline, LoaderTransactionStatus.Applied, plan.Entries, expected);
        var path = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        LoaderTransactionStateService.SaveAtomic(path, state);

        var loaded = LoaderTransactionStateService.LoadAndValidate(
            path,
            roots,
            state.ExpectedFingerprint,
            baseline,
            [LoaderTransactionStatus.Applied]);

        LoaderTransactionStateService.VerifyAppliedProfile(roots, loaded);
    }

    [Fact]
    public void BootstrapMayCreateKnownEmptyPluginAndPatcherDirectoriesButNotFilesInsideThem()
    {
        var applied = new CopyManifest(
            [new FileManifestEntry("game.txt", 4, new string('a', 64))],
            ["BepInEx"]);
        var current = new CopyManifest(
            [
                new FileManifestEntry("game.txt", 4, new string('a', 64)),
                new FileManifestEntry("BepInEx/LogOutput.log", 2, new string('b', 64)),
                new FileManifestEntry("BepInEx/config/BepInEx.cfg", 3, new string('c', 64)),
                new FileManifestEntry("BepInEx/cache/chainloader_typeloader.dat", 4, new string('d', 64)),
                new FileManifestEntry("BepInEx/cache/harmony_interop_cache.dat", 5, new string('e', 64))
            ],
            ["BepInEx", "BepInEx/config", "BepInEx/cache", "BepInEx/plugins", "BepInEx/patchers"]);

        var generated = LoaderTransactionStateService.CaptureGeneratedEvidence(
            applied,
            current,
            out var generatedDirectories);

        Assert.Equal(4, generated.Count);
        Assert.Contains("BepInEx/plugins", generatedDirectories);
        Assert.Contains("BepInEx/patchers", generatedDirectories);

        var withPluginFile = new CopyManifest(
            [new FileManifestEntry("game.txt", 4, new string('a', 64)), new FileManifestEntry("BepInEx/plugins/plugin.dll", 1, new string('b', 64))],
            current.Directories);

        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.CaptureGeneratedEvidence(
            applied,
            withPluginFile,
            out _));
    }

    [Fact]
    public void ValidLaunchObservedStateRequiresAndRetainsBootstrapEvidence()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "game.txt"), "game");
        fixture.WriteArchive(("BepInEx/core.dll", "loader"));
        var extracted = ArchiveSafetyService.Extract(fixture.ArchivePath, roots.ExtractedLoaderRoot);
        var baseline = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);
        LoaderTransactionService.Apply(roots, plan, extracted);
        var state = CreateState(
            roots,
            baseline,
            LoaderTransactionStatus.LaunchObserved,
            plan.Entries,
            LoaderTransactionService.BuildExpectedAppliedManifest(baseline, extracted)) with
        {
            LaunchEvidence = new LoaderBootstrapEvidence("5.4.23.5", true, true, 0, 0, 0, 0)
        };
        var path = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        LoaderTransactionStateService.SaveAtomic(path, state);

        var loaded = LoaderTransactionStateService.LoadAndValidate(
            path,
            roots,
            state.ExpectedFingerprint,
            baseline,
            [LoaderTransactionStatus.LaunchObserved]);

        LoaderTransactionStateService.VerifyAppliedProfile(roots, loaded);
        Assert.True(loaded.LaunchEvidence!.MeetsBootstrapCriteria);
    }

    [Fact]
    public void LaunchObservedStateWithoutBootstrapEvidenceIsRejected()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        var baseline = new CopyManifest([new FileManifestEntry("game.txt", 4, new string('c', 64))], []);
        var state = CreateState(roots, baseline, LoaderTransactionStatus.LaunchObserved, [
            new TransactionEntry("BepInEx/core.dll", TransactionChangeKind.NewFile, null, new string('a', 64), null)
        ]);
        var path = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        LoaderTransactionStateService.SaveAtomic(path, state);

        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.LoadAndValidate(
            path,
            roots,
            state.ExpectedFingerprint,
            baseline,
            [LoaderTransactionStatus.LaunchObserved]));
    }

    [Fact]
    public void TransactionStateRejectsOtherFingerprintOrBaseline()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        var baseline = new CopyManifest([new FileManifestEntry("game.txt", 4, new string('c', 64))], []);
        var state = CreateState(roots, baseline, LoaderTransactionStatus.Applied, [
            new TransactionEntry("BepInEx/core.dll", TransactionChangeKind.NewFile, null, new string('a', 64), null)
        ]);
        var path = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        LoaderTransactionStateService.SaveAtomic(path, state);

        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.LoadAndValidate(
            path,
            roots,
            new string('b', 64),
            baseline,
            [LoaderTransactionStatus.Applied]));

        var otherBaseline = new CopyManifest([new FileManifestEntry("other.txt", 5, new string('d', 64))], []);
        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.LoadAndValidate(
            path,
            roots,
            state.ExpectedFingerprint,
            otherBaseline,
            [LoaderTransactionStatus.Applied]));
    }

    [Fact]
    public void PersistedTransactionRejectsTraversalAbsoluteDuplicateAndBackupPaths()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        var invalidEntrySets = new IReadOnlyList<TransactionEntry>[]
        {
            [new TransactionEntry("../outside.txt", TransactionChangeKind.NewFile, null, new string('a', 64), null)],
            [new TransactionEntry(Path.GetFullPath("outside.txt"), TransactionChangeKind.NewFile, null, new string('a', 64), null)],
            [
                new TransactionEntry("BepInEx/core.dll", TransactionChangeKind.NewFile, null, new string('a', 64), null),
                new TransactionEntry("BepInEx/core.dll", TransactionChangeKind.NewFile, null, new string('b', 64), null)
            ],
            [new TransactionEntry("Game.dll", TransactionChangeKind.Overwrite, new string('c', 64), new string('d', 64), "../outside.bak")]
        };

        foreach (var entries in invalidEntrySets)
        {
            Assert.Throws<SmokeTestException>(() => LoaderTransactionService.ValidatePersistedEntries(roots, entries));
        }
    }

    [Fact]
    public void MaliciousRollbackCannotTouchAFileOutsideTheDisposableProfile()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        var outside = Path.Combine(fixture.Root, "outside.txt");
        File.WriteAllText(outside, "keep");
        var plan = new TransactionPlan(
            roots.ExtractedLoaderRoot,
            [new TransactionEntry("../outside.txt", TransactionChangeKind.NewFile, null, new string('a', 64), null)]);

        Assert.Throws<SmokeTestException>(() => LoaderTransactionService.Rollback(roots, plan));
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    [Fact]
    public void AppliedProfileRejectsMissingOrChangedLoaderFiles()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "game.txt"), "game");
        fixture.WriteArchive(("BepInEx/core.dll", "loader"));
        var extracted = ArchiveSafetyService.Extract(fixture.ArchivePath, roots.ExtractedLoaderRoot);
        var baseline = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);
        LoaderTransactionService.Apply(roots, plan, extracted);
        var state = CreateState(
            roots,
            baseline,
            LoaderTransactionStatus.Applied,
            plan.Entries,
            LoaderTransactionService.BuildExpectedAppliedManifest(baseline, extracted));
        File.Delete(Path.Combine(roots.CleanGameRoot, "BepInEx", "core.dll"));

        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.VerifyAppliedProfile(roots, state));
    }

    [Fact]
    public void AppliedProfileRejectsUnrelatedAddedFiles()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "game.txt"), "game");
        fixture.WriteArchive(("BepInEx/core.dll", "loader"));
        var extracted = ArchiveSafetyService.Extract(fixture.ArchivePath, roots.ExtractedLoaderRoot);
        var baseline = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        var plan = LoaderTransactionService.Prepare(roots, extracted);
        LoaderTransactionService.Apply(roots, plan, extracted);
        var state = CreateState(
            roots,
            baseline,
            LoaderTransactionStatus.Applied,
            plan.Entries,
            LoaderTransactionService.BuildExpectedAppliedManifest(baseline, extracted));
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "unrelated.txt"), "unexpected");

        Assert.Throws<SmokeTestException>(() => LoaderTransactionStateService.VerifyAppliedProfile(roots, state));
    }

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

    private static LoaderTransactionState CreateState(
        SmokeTestRoots roots,
        CopyManifest baseline,
        LoaderTransactionStatus status,
        IReadOnlyList<TransactionEntry> entries,
        CopyManifest? applied = null)
        => new(
            LoaderTransactionStateService.SchemaVersion,
            LoaderTransactionStateService.TaskVersion,
            new string('a', 64),
            InstallationCopyService.ComputeManifestIdentity(baseline),
            "BepInEx_win_x64_5.4.23.5.zip",
            new string('b', 64),
            status,
            applied ?? baseline,
            entries,
            []);
}

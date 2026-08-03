using ThroneForge.Discovery;
using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class Task3HardeningTests
{
    [Fact]
    public void CompleteManifestComparisonReportsAddedRemovedAndChangedFiles()
    {
        var expected = new CopyManifest(
            [new FileManifestEntry("same.txt", 1, "a"), new FileManifestEntry("changed.txt", 1, "a")],
            ["profiles"]);
        var actual = new CopyManifest(
            [new FileManifestEntry("same.txt", 1, "a"), new FileManifestEntry("changed.txt", 2, "b"), new FileManifestEntry("added.txt", 1, "c")],
            ["profiles", "unexpected"]);

        var result = InstallationCopyService.CompareManifests(expected, actual);

        Assert.Equal(ManifestVerificationStatus.ChangedFiles, result.Status);
        Assert.Contains(result.ChangedFiles, item => item.RelativePath == "changed.txt");
        Assert.Contains(result.AddedFiles, item => item.RelativePath == "added.txt");
        Assert.Contains("unexpected", result.UnexpectedDirectories);
        Assert.False(result.Matches);
    }

    [Fact]
    public void RestoreManifestVerifiesNonFingerprintFilesToo()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "game.txt"), "game");
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "non-fingerprint.txt"), "baseline");
        var baseline = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        File.WriteAllText(Path.Combine(roots.CleanGameRoot, "non-fingerprint.txt"), "changed");

        var result = InstallationCopyService.RestoreFilesToManifest(roots.CleanGameRoot, baseline);

        Assert.False(result.Matches);
        Assert.Contains(result.ChangedFiles, item => item.RelativePath == "non-fingerprint.txt");
    }

    [Fact]
    public void FreshProfileRejectsExistingCleanGame()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);

        Assert.Throws<SmokeTestException>(() => DisposableProfileBaselineService.RequireFreshProfile(roots));
    }

    [Fact]
    public void ResumeRequiresAValidSchemaBackedBaseline()
    {
        using var fixture = new SmokeTestFixture();
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);
        Directory.CreateDirectory(roots.CleanGameRoot);
        var manifest = new CopyManifest([new FileManifestEntry("game.txt", 4, "hash")], []);
        var baselinePath = Path.Combine(fixture.Root, "baseline.json");
        File.WriteAllText(baselinePath, "{\"SchemaVersion\":\"wrong\"}");

        Assert.Throws<SmokeTestException>(() => DisposableProfileBaselineService.LoadAndValidateResume(
            baselinePath,
            new string('a', 64),
            manifest,
            manifest,
            SmokeTestReadiness.ReadyForReversibleTest,
            []));
    }

    [Fact]
    public void ValidManifestBackedResumeIsAccepted()
    {
        using var fixture = new SmokeTestFixture();
        var original = new CopyManifest([new FileManifestEntry("game.txt", 4, "hash")], []);
        var current = new CopyManifest([new FileManifestEntry("game.txt", 4, "hash")], []);
        var baselinePath = Path.Combine(fixture.Root, "baseline.json");
        DisposableProfileBaselineService.Save(
            baselinePath,
            new DisposableProfileBaseline(
                DisposableProfileBaselineService.SchemaVersion,
                DisposableProfileBaselineService.TaskVersion,
                new string('a', 64),
                original,
                current));

        var result = DisposableProfileBaselineService.LoadAndValidateResume(
            baselinePath,
            new string('a', 64),
            original,
            current,
            SmokeTestReadiness.ReadyForReversibleTest,
            []);

        Assert.Equal(DisposableProfileBaselineService.SchemaVersion, result.SchemaVersion);
    }

    [Fact]
    public void ResumeRejectsAddedAndChangedNonFingerprintFiles()
    {
        using var fixture = new SmokeTestFixture();
        var baseline = new CopyManifest([new FileManifestEntry("game.txt", 4, "hash")], []);
        var baselinePath = Path.Combine(fixture.Root, "baseline.json");
        DisposableProfileBaselineService.Save(
            baselinePath,
            new DisposableProfileBaseline(
                DisposableProfileBaselineService.SchemaVersion,
                DisposableProfileBaselineService.TaskVersion,
                new string('a', 64),
                baseline,
                baseline));

        var changed = new CopyManifest(
            [new FileManifestEntry("game.txt", 4, "changed"), new FileManifestEntry("extra.txt", 1, "extra")],
            []);

        Assert.Throws<SmokeTestException>(() => DisposableProfileBaselineService.LoadAndValidateResume(
            baselinePath,
            new string('a', 64),
            baseline,
            changed,
            SmokeTestReadiness.ReadyForReversibleTest,
            []));
    }

    [Fact]
    public void PostApplyLaunchFailureRollsBack()
    {
        var rollbackCalls = 0;
        var original = new SmokeTestException("synthetic launch failure");
        var result = SmokeTestPostApplyGuard.Execute(
            launch: () => throw original,
            readLog: () => "never",
            parseLog: _ => throw new InvalidOperationException(),
            classify: _ => SmokeTestOutcome.Passed,
            rollback: () =>
            {
                rollbackCalls++;
                return true;
            });

        Assert.Equal(SmokeTestOutcome.Failed, result.Outcome);
        Assert.Equal(SmokeTestRollbackState.RollbackSucceeded, result.RollbackState);
        Assert.Equal(1, rollbackCalls);
        Assert.Same(original, result.OperationException);
    }

    [Fact]
    public void PostApplyLogReadFailureRollsBack()
    {
        var rollbackCalls = 0;
        var result = SmokeTestPostApplyGuard.Execute(
            launch: () => SuccessfulLaunch(),
            readLog: () => throw new IOException("synthetic log failure"),
            parseLog: _ => throw new InvalidOperationException(),
            classify: _ => SmokeTestOutcome.Passed,
            rollback: () =>
            {
                rollbackCalls++;
                return true;
            });

        Assert.Equal(SmokeTestRollbackState.RollbackSucceeded, result.RollbackState);
        Assert.Equal(1, rollbackCalls);
    }

    [Fact]
    public void PostApplyParseAndClassificationFailuresRollBack()
    {
        foreach (var mode in new[] { "parse", "classify" })
        {
            var rollbackCalls = 0;
            var result = SmokeTestPostApplyGuard.Execute(
                launch: SuccessfulLaunch,
                readLog: () => "synthetic",
                parseLog: _ => mode == "parse"
                    ? throw new InvalidDataException("synthetic parse failure")
                    : new LoaderLogSummary("5.4.23.5", true, true, true, 0, 0, 0, 0, [], true),
                classify: _ => throw new InvalidOperationException("synthetic classification failure"),
                rollback: () =>
                {
                    rollbackCalls++;
                    return true;
                });

            Assert.Equal(SmokeTestRollbackState.RollbackSucceeded, result.RollbackState);
            Assert.Equal(1, rollbackCalls);
        }
    }

    [Fact]
    public void RollbackFailureForcesFailedOutcome()
    {
        var result = SmokeTestPostApplyGuard.Execute(
            launch: SuccessfulLaunch,
            readLog: () => "synthetic",
            parseLog: _ => new LoaderLogSummary("5.4.23.5", true, true, true, 0, 0, 0, 0, [], true),
            classify: _ => SmokeTestOutcome.Passed,
            rollback: () => false);

        Assert.Equal(SmokeTestOutcome.Failed, result.Outcome);
        Assert.Equal(SmokeTestRollbackState.RollbackFailed, result.RollbackState);
    }

    [Fact]
    public void ReportPreparationFailureStillRollsBack()
    {
        var rollbackCalls = 0;
        var result = SmokeTestPostApplyGuard.Execute(
            launch: SuccessfulLaunch,
            readLog: () => "synthetic",
            parseLog: _ => new LoaderLogSummary("5.4.23.5", true, true, true, 0, 0, 0, 0, [], true),
            classify: _ => SmokeTestOutcome.Passed,
            rollback: () =>
            {
                rollbackCalls++;
                return true;
            },
            prepareReport: () => throw new IOException("synthetic report-write failure"));

        Assert.Equal(SmokeTestRollbackState.RollbackSucceeded, result.RollbackState);
        Assert.Equal(1, rollbackCalls);
    }

    [Fact]
    public void ManualClosureLeavesFilesActiveAndCreatesRecoveryState()
    {
        var markerCalls = 0;
        var rollbackCalls = 0;
        var result = SmokeTestPostApplyGuard.Execute(
            launch: () => SuccessfulLaunch() with { RequiresManualClosure = true, Exited = false },
            readLog: () => "never",
            parseLog: _ => throw new InvalidOperationException(),
            classify: _ => SmokeTestOutcome.Passed,
            rollback: () =>
            {
                rollbackCalls++;
                return true;
            },
            writeRecoveryMarker: () => markerCalls++);

        Assert.Equal(SmokeTestOutcome.Inconclusive, result.Outcome);
        Assert.Equal(SmokeTestRollbackState.ManualClosureRequired, result.RollbackState);
        Assert.Equal(1, markerCalls);
        Assert.Equal(0, rollbackCalls);
    }

    [Fact]
    public void ExplicitRecoveryClearRemovesMarker()
    {
        using var fixture = new SmokeTestFixture();
        var marker = Path.Combine(fixture.Root, "recovery-marker.json");

        SmokeTestRecoveryMarkerService.Write(marker);
        SmokeTestRecoveryMarkerService.Clear(marker);

        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void CommittedReportPathIsDerivedAndContained()
    {
        using var fixture = new SmokeTestFixture();
        Directory.CreateDirectory(Path.Combine(fixture.RepositoryRoot, "docs", "discovery"));
        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);

        var path = SmokeTestPathValidator.ValidateCommittedReportPath(roots, new string('a', 64));

        Assert.StartsWith(Path.Combine(fixture.RepositoryRoot, "docs", "discovery"), path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("-loader-smoke-test.md", path, StringComparison.Ordinal);
    }

    [Fact]
    public void CommittedReportPathRejectsReparsePointParent()
    {
        using var fixture = new SmokeTestFixture();
        var realDocs = Path.Combine(fixture.Root, "real-docs");
        Directory.CreateDirectory(Path.Combine(realDocs, "discovery"));
        var linkedDocs = Path.Combine(fixture.RepositoryRoot, "docs");
        Directory.CreateDirectory(fixture.RepositoryRoot);
        try
        {
            Directory.CreateSymbolicLink(linkedDocs, realDocs);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var roots = SmokeTestPathValidator.ValidateRoots(fixture.RepositoryRoot, fixture.GameRoot, fixture.ExperimentRoot);

        Assert.Throws<SmokeTestException>(() => SmokeTestPathValidator.ValidateCommittedReportPath(roots, new string('a', 64)));
    }

    [Fact]
    public void CliRejectsArbitraryReportPathOption()
    {
        using var fixture = new SmokeTestFixture();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = LoaderSmokeTestCli.Run(
            ["Plan", "--game-path", fixture.GameRoot, "--experiment-root", fixture.ExperimentRoot,
             "--expected-fingerprint", new string('a', 64), "--repository-root", fixture.RepositoryRoot,
             "--report-path", Path.Combine(fixture.GameRoot, "forbidden.md")],
            stdout,
            stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Unknown option", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.GameRoot, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static LaunchObservationResult SuccessfulLaunch()
        => new(true, true, true, 0, true, false, TimeSpan.FromMilliseconds(1), "stable");
}

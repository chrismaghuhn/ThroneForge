using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class LoaderOnlyProfileVerificationTests
{
    [Fact]
    public void ChangedLogOutputLogIsAcceptedWhenThePathRemainsPresent()
    {
        var expected = Manifest(includeLog: "BepInEx/LogOutput.log");
        var actual = Manifest(includeLog: "BepInEx/LogOutput.log", logHash: new string('b', 64));

        Assert.True(LoaderOnlyProfileVerificationService.Compare(expected, actual).Matches);
    }

    [Fact]
    public void ChangedLogOutputTextIsAcceptedWhenThePathRemainsPresent()
    {
        var expected = Manifest(includeLog: "BepInEx/LogOutput.txt");
        var actual = Manifest(includeLog: "BepInEx/LogOutput.txt", logHash: new string('c', 64));

        Assert.True(LoaderOnlyProfileVerificationService.Compare(expected, actual).Matches);
    }

    [Fact]
    public void MissingRecognizedLogIsRejected()
    {
        var expected = Manifest(includeLog: "BepInEx/LogOutput.log");
        var actual = Manifest();

        Assert.False(LoaderOnlyProfileVerificationService.Compare(expected, actual).Matches);
    }

    [Fact]
    public void AdditionalRecognizedLogIsRejected()
    {
        var expected = Manifest(includeLog: "BepInEx/LogOutput.log");
        var actual = Manifest(includeLog: "BepInEx/LogOutput.log", additionalLog: "BepInEx/LogOutput.txt");

        Assert.False(LoaderOnlyProfileVerificationService.Compare(expected, actual).Matches);
    }

    [Fact]
    public void ChangedLoaderCoreFileIsRejected()
    {
        var expected = Manifest();
        var actual = Manifest(coreHash: new string('d', 64));

        Assert.False(LoaderOnlyProfileVerificationService.Compare(expected, actual).Matches);
    }

    [Fact]
    public void ChangedConfigurationFileIsRejected()
    {
        var expected = Manifest(includeConfig: true);
        var actual = Manifest(includeConfig: true, configHash: new string('e', 64));

        Assert.False(LoaderOnlyProfileVerificationService.Compare(expected, actual).Matches);
    }

    [Fact]
    public void AdditionalArbitraryFileIsRejected()
    {
        var expected = Manifest();
        var actual = Manifest(additionalFile: "BepInEx/cache/unexpected.bin");

        Assert.False(LoaderOnlyProfileVerificationService.Compare(expected, actual).Matches);
    }

    [Fact]
    public void PluginRemovalWithChangedLifecycleLogPassesLoaderOnlyComparison()
    {
        var loaderOnly = Manifest(includeLog: "BepInEx/LogOutput.log");
        var afterLifecycle = Manifest(
            includeLog: "BepInEx/LogOutput.log",
            logHash: new string('f', 64));

        Assert.True(LoaderOnlyProfileVerificationService.Compare(loaderOnly, afterLifecycle).Matches);
        Assert.True(InstallationCopyService.CompareManifests(loaderOnly, afterLifecycle).Matches == false);
    }

    [Fact]
    public void CompleteBaselineComparisonStillRejectsVolatileLogChanges()
    {
        var baseline = Manifest(includeLog: "BepInEx/LogOutput.log");
        var afterRollback = Manifest(includeLog: "BepInEx/LogOutput.log", logHash: new string('a', 64));

        Assert.False(InstallationCopyService.CompareManifests(baseline, afterRollback).Matches);
    }

    private static CopyManifest Manifest(
        string? includeLog = null,
        string? logHash = null,
        string? additionalLog = null,
        string? coreHash = null,
        bool includeConfig = false,
        string? configHash = null,
        string? additionalFile = null)
    {
        var files = new List<FileManifestEntry>
        {
            new("BepInEx/core/BepInEx.dll", 3, coreHash ?? new string('1', 64))
        };
        if (includeLog is not null)
        {
            files.Add(new(includeLog, 3, logHash ?? new string('2', 64)));
        }

        if (additionalLog is not null)
        {
            files.Add(new(additionalLog, 3, new string('3', 64)));
        }

        if (includeConfig)
        {
            files.Add(new("BepInEx/config/BepInEx.cfg", 3, configHash ?? new string('4', 64)));
        }

        if (additionalFile is not null)
        {
            files.Add(new(additionalFile, 3, new string('5', 64)));
        }

        return new CopyManifest(files, ["BepInEx", "BepInEx/core"]);
    }
}

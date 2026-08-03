using ThroneForge.LoaderSmokeTest;
using System.IO.Compression;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class ArchiveSafetyServiceTests
{
    [Theory]
    [InlineData("/absolute.txt")]
    [InlineData("../escape.txt")]
    [InlineData("folder/../../escape.txt")]
    [InlineData("C:/device.txt")]
    [InlineData("folder/file.txt:stream")]
    [InlineData("folder\\file.txt")]
    public void RejectsUnsafeArchiveEntryNames(string name)
    {
        using var fixture = new SmokeTestFixture();
        fixture.WriteArchive((name, "unsafe"));

        Assert.Throws<SmokeTestException>(() => ArchiveSafetyService.Inspect(fixture.ArchivePath));
    }

    [Fact]
    public void RejectsDuplicateNormalizedDestinations()
    {
        using var fixture = new SmokeTestFixture();
        fixture.WriteArchive(("same.txt", "one"), ("same.txt", "two"));

        Assert.Throws<SmokeTestException>(() => ArchiveSafetyService.Inspect(fixture.ArchivePath));
    }

    [Fact]
    public void ExtractsOnlyInsideValidatedTargetAndProducesDeterministicManifest()
    {
        using var fixture = new SmokeTestFixture();
        fixture.WriteArchive(("BepInEx/core.dll", "core"), ("doorstop_config.ini", "config"));
        var first = ArchiveSafetyService.Extract(fixture.ArchivePath, Path.Combine(fixture.ExperimentRoot, "one"));
        var second = ArchiveSafetyService.Extract(fixture.ArchivePath, Path.Combine(fixture.ExperimentRoot, "two"));

        Assert.Equal(first.Manifest, second.Manifest);
        Assert.Equal("core", File.ReadAllText(Path.Combine(fixture.ExperimentRoot, "one", "BepInEx", "core.dll")));
    }

    [Fact]
    public void RejectsConfiguredExpandedSizeLimit()
    {
        using var fixture = new SmokeTestFixture();
        fixture.WriteArchive(("large.txt", "0123456789"));

        Assert.Throws<SmokeTestException>(() => ArchiveSafetyService.Inspect(
            fixture.ArchivePath,
            new ArchiveSafetyLimits(MaximumEntries: 10, MaximumExpandedBytes: 5)));
    }

    [Fact]
    public void RejectsArchiveSymlinkEntry()
    {
        using var fixture = new SmokeTestFixture();
        using (var archive = ZipFile.Open(fixture.ArchivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("link.txt");
            entry.ExternalAttributes = 0xA000 << 16;
            using var writer = new StreamWriter(entry.Open());
            writer.Write("target");
        }

        Assert.Throws<SmokeTestException>(() => ArchiveSafetyService.Inspect(fixture.ArchivePath));
    }

    [Fact]
    public void RejectsArchiveEntryCountLimit()
    {
        using var fixture = new SmokeTestFixture();
        fixture.WriteArchive(("one.txt", "one"), ("two.txt", "two"));

        Assert.Throws<SmokeTestException>(() => ArchiveSafetyService.Inspect(
            fixture.ArchivePath,
            new ArchiveSafetyLimits(MaximumEntries: 1)));
    }
}

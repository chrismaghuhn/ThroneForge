using ThroneForge.LoaderSmokeTest;
using Xunit;

namespace ThroneForge.LoaderSmokeTest.Tests;

public sealed class SmokeTestReportWriterTests
{
    [Fact]
    public void ReportSanitizationRemovesAbsolutePathsAndRawLogContent()
    {
        var report = SmokeTestReportWriter.BuildSanitizedReport(new SmokeTestReportData(
            "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d",
            "C:\\Users\\example\\Thronefall",
            "C:\\Users\\example\\Experiments",
            SmokeTestOutcome.Inconclusive,
            "[Info] raw log should not be retained",
            "5.4.23.5"));

        Assert.DoesNotContain("C:\\Users\\example", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw log should not be retained", report, StringComparison.Ordinal);
        Assert.Contains("Inconclusive", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AtomicReportWriteLeavesNoTemporaryFile()
    {
        using var fixture = new SmokeTestFixture();
        var reportPath = Path.Combine(fixture.ExperimentRoot, "evidence", "report.md");
        var markdown = SmokeTestReportWriter.BuildSanitizedReport(new SmokeTestReportData(
            "a", "private", "private", SmokeTestOutcome.Inconclusive, "raw", null));

        SmokeTestReportWriter.WriteAtomic(reportPath, markdown, overwrite: false);

        Assert.True(File.Exists(reportPath));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(reportPath)!, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void DetailedReportSeparatesLaunchResultsAndUsesSanitizedLogSummary()
    {
        var report = SmokeTestReportWriter.BuildReport(new SmokeTestDetailedReport(
            "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7",
            "m1-loader-smoke-test-v1",
            DateTimeOffset.UtcNow,
            SmokeTestOutcome.Passed,
            "Original verified.",
            "Copy verified.",
            "Baseline launch succeeded.",
            "Official release verified.",
            "BepInEx_win_x64_5.4.23.5.zip",
            "352395699",
            "639118",
            "Matched official digest.",
            "82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4",
            "Extraction verified.",
            "Transaction applied.",
            "Loader launch succeeded.",
            "Equivalent initialization evidence.",
            new LoaderLogSummary("5.4.23.5", false, true, true, 0, 0, 0, 0, [], true),
            "Rollback verified.",
            "Original remained unchanged.",
            [],
            [],
            "Plugin and game API compatibility remain unknown.",
            "M1 task 4."));

        Assert.Contains("Baseline launch succeeded.", report, StringComparison.Ordinal);
        Assert.Contains("Loader launch succeeded.", report, StringComparison.Ordinal);
        Assert.Contains("## Sanitized log summary", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Original absolute path", report, StringComparison.OrdinalIgnoreCase);
    }
}

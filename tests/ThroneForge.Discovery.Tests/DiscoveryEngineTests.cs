using ThroneForge.Discovery;
using Xunit;

namespace ThroneForge.Discovery.Tests;

public sealed class DiscoveryEngineTests
{
    [Fact]
    public void ClassifiesMonoFromMultipleCompatibleIndicators()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();

        var result = Inspect(fixture);

        Assert.Equal(BackendClassification.Mono, result.Backend);
        Assert.Equal(ExecutableArchitecture.X64, result.ExecutableArchitecture);
        Assert.Contains(result.DetectedEvidence, item => item.RelativePath.EndsWith("/Managed", StringComparison.Ordinal));
        Assert.Contains(result.SelectedFiles, item => item.RelativePath.EndsWith("Assembly-CSharp.dll", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassifiesIl2CppFromMultipleCompatibleIndicators()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateIl2CppLayout();

        var result = Inspect(fixture);

        Assert.Equal(BackendClassification.IL2CPP, result.Backend);
        Assert.Equal(ExecutableArchitecture.X64, result.ExecutableArchitecture);
        Assert.Contains(result.DetectedEvidence, item => item.RelativePath == "GameAssembly.dll");
        Assert.Contains(result.SelectedFiles, item => item.RelativePath.EndsWith("global-metadata.dat", StringComparison.Ordinal));
    }

    [Fact]
    public void ClassifiesConflictingStrongEvidenceAsAmbiguous()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        fixture.CreateIl2CppLayout();

        var result = Inspect(fixture);

        Assert.Equal(BackendClassification.Ambiguous, result.Backend);
        Assert.Contains(result.MissingOrConflictingEvidence, item => item.Contains("conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClassifiesInsufficientEvidenceAsUnknown()
    {
        using var fixture = new DiscoveryTestFixture();
        Directory.CreateDirectory(fixture.DataRoot);

        var result = Inspect(fixture);

        Assert.Equal(BackendClassification.Unknown, result.Backend);
        Assert.Contains(result.MissingOrConflictingEvidence, item => item.Contains("insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractsUnityVersionOnlyFromLocalEvidence()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        fixture.CreateUnityVersion();

        var result = Inspect(fixture);

        Assert.Equal("2022.3.12f1", result.UnityVersion);
        Assert.Contains(result.DetectedEvidence, item => item.Category == "Unity version");
    }

    [Fact]
    public void RejectsMissingRelativeAndFileRoots()
    {
        using var fixture = new DiscoveryTestFixture();
        var engine = new DiscoveryEngine();

        Assert.Throws<DiscoveryException>(() => engine.Inspect(new DiscoveryRequest(
            Path.Combine(fixture.Root, "missing"), fixture.OutputRoot)));
        Assert.Throws<DiscoveryException>(() => engine.Inspect(new DiscoveryRequest(
            ".", fixture.OutputRoot)));
        Assert.Throws<DiscoveryException>(() => engine.Inspect(new DiscoveryRequest(
            Path.Combine(fixture.Root, "file.txt"), fixture.OutputRoot)));
        Assert.Throws<DiscoveryException>(() => engine.Inspect(new DiscoveryRequest(
            string.Empty, fixture.OutputRoot)));
    }

    [Fact]
    public void DoesNotTraverseReparsePointOutsideTheRoot()
    {
        using var fixture = new DiscoveryTestFixture();
        using var outside = new DiscoveryTestFixture();
        outside.CreateIl2CppLayout();
        if (!fixture.TryCreateDirectoryLink("escaped", outside.Root))
        {
            return;
        }

        var result = Inspect(fixture);

        Assert.Equal(BackendClassification.Unknown, result.Backend);
        Assert.DoesNotContain(result.DetectedEvidence, item => item.RelativePath.Contains("escaped", StringComparison.Ordinal));
    }

    [Fact]
    public void ProducesTheSameFingerprintForEquivalentFixtures()
    {
        using var first = new DiscoveryTestFixture();
        using var second = new DiscoveryTestFixture();
        first.CreateMonoLayout();
        second.CreateMonoLayout();

        var firstResult = Inspect(first);
        var secondResult = Inspect(second);

        Assert.Equal(firstResult.Fingerprint, secondResult.Fingerprint);
    }

    [Fact]
    public void ChangesFingerprintWhenSelectedMetadataChanges()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        var first = Inspect(fixture);
        File.AppendAllText(Path.Combine(fixture.DataRoot, "Managed", "Assembly-CSharp.dll"), "changed");

        var second = Inspect(fixture, outputName: "reports-2");

        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void ReportIsSanitizedAndContainsRequiredSections()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        fixture.CreateUnityVersion();

        var result = Inspect(fixture);

        Assert.DoesNotContain(fixture.Root, result.ReportMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.UserName, result.ReportMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("# Thronefall Discovery Report", result.ReportMarkdown, StringComparison.Ordinal);
        Assert.Contains("## Privacy and sanitization statement", result.ReportMarkdown, StringComparison.Ordinal);
        Assert.Contains("SHA-256", result.ReportMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", result.ReportMarkdown, StringComparison.Ordinal);

        foreach (var heading in new[]
        {
            "## Fingerprint",
            "## Discovery tool version",
            "## Fingerprint algorithm version",
            "## Discovery timestamp in UTC",
            "## Backend classification",
            "## Executable architecture",
            "## Unity-version evidence",
            "## Detected evidence",
            "## Missing or conflicting evidence",
            "## Relevant files using relative paths only",
            "## Selected file sizes and SHA-256 values",
            "## Compatibility conclusions",
            "## Unverified assumptions",
            "## Recommended next investigation"
        })
        {
            Assert.Contains(heading, result.ReportMarkdown, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExistingReportIsNotOverwrittenWithoutExplicitOverwrite()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        var first = Inspect(fixture);
        var original = File.ReadAllText(first.ReportPath);

        var exception = Assert.Throws<DiscoveryException>(() => Inspect(fixture));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, File.ReadAllText(first.ReportPath));
        Assert.Empty(Directory.GetFiles(fixture.OutputRoot, "*.tmp"));
    }

    [Fact]
    public void AtomicReportWritingLeavesOnlyTheFinalReport()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();

        var result = Inspect(fixture);

        Assert.True(File.Exists(result.ReportPath));
        Assert.Equal(result.ReportMarkdown, File.ReadAllText(result.ReportPath));
        Assert.Empty(Directory.GetFiles(fixture.OutputRoot, "*.tmp"));
    }

    private static DiscoveryResult Inspect(DiscoveryTestFixture fixture, string? outputName = null)
    {
        var outputRoot = outputName is null
            ? fixture.OutputRoot
            : Path.Combine(fixture.Root, outputName);
        return new DiscoveryEngine().Inspect(new DiscoveryRequest(
            fixture.Root,
            outputRoot,
            DiscoveryTimestampUtc: new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));
    }
}

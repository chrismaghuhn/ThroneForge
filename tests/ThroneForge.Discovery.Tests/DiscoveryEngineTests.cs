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
    public void RejectsOutputRootEqualToGameRootBeforeWriting()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();

        var exception = Assert.Throws<DiscoveryException>(() => new DiscoveryEngine().Inspect(new DiscoveryRequest(
            fixture.Root,
            fixture.Root)));

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.md"));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp"));
    }

    [Fact]
    public void RejectsOutputRootBelowGameRootBeforeWriting()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        var outputRoot = Path.Combine(fixture.Root, "reports");

        var exception = Assert.Throws<DiscoveryException>(() => new DiscoveryEngine().Inspect(new DiscoveryRequest(
            fixture.Root,
            outputRoot)));

        Assert.Contains("outside", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void AllowsSimilarlyPrefixedSiblingOutputRoot()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        var outputRoot = fixture.CreateExternalOutputRoot($"{Path.GetFileName(fixture.Root)}-Reports");

        var result = new DiscoveryEngine().Inspect(new DiscoveryRequest(fixture.Root, outputRoot));

        Assert.True(File.Exists(result.ReportPath));
        Assert.DoesNotContain(
            Path.GetFullPath(fixture.Root) + Path.DirectorySeparatorChar,
            result.ReportPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsOutputRootThatIsAnExistingReparsePointOrHasReparseParent()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        if (!fixture.TryCreateExternalDirectoryLink(out var linkPath))
        {
            return;
        }

        var directException = Assert.Throws<DiscoveryException>(() => new DiscoveryEngine().Inspect(new DiscoveryRequest(
            fixture.Root,
            linkPath)));
        var parentException = Assert.Throws<DiscoveryException>(() => new DiscoveryEngine().Inspect(new DiscoveryRequest(
            fixture.Root,
            Path.Combine(linkPath, "reports"))));

        Assert.Contains("reparse", directException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reparse", parentException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsInvalidOutputPathWithoutLeakingThePath()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        const string invalidOutputPath = "invalid\0output";

        var exception = Assert.Throws<DiscoveryException>(() => new DiscoveryEngine().Inspect(new DiscoveryRequest(
            fixture.Root,
            invalidOutputPath)));

        Assert.DoesNotContain(invalidOutputPath, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectsRenamedInstallationDataBaseExecutable()
    {
        using var fixture = new DiscoveryTestFixture();
        var installation = Path.Combine(fixture.Root, "RenamedThronefall");
        Directory.CreateDirectory(installation);
        var dataRoot = Path.Combine(installation, "RenamedThronefall_Data", "Managed");
        Directory.CreateDirectory(Path.Combine(installation, "RenamedThronefall_Data", "MonoBleedingEdge"));
        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(Path.Combine(dataRoot, "Assembly-CSharp.dll"), "synthetic mono assembly");
        DiscoveryTestFixture.WriteMinimalPe(Path.Combine(installation, "RenamedThronefall.exe"), 0x8664);

        var result = new DiscoveryEngine().Inspect(new DiscoveryRequest(installation, fixture.OutputRoot));

        Assert.Contains(result.DetectedEvidence, item => item.RelativePath == "RenamedThronefall.exe");
        Assert.Equal(ExecutableArchitecture.X64, result.ExecutableArchitecture);
    }

    [Fact]
    public void PrefersDataBaseExecutableOverAlphabeticallyEarlierCrashHandler()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        DiscoveryTestFixture.WriteMinimalPe(Path.Combine(fixture.Root, "a-crashhandler.exe"), 0x014C);

        var result = Inspect(fixture);

        Assert.Contains(result.DetectedEvidence, item => item.RelativePath == "Thronefall.exe");
        Assert.DoesNotContain(result.DetectedEvidence, item => item.RelativePath == "a-crashhandler.exe");
        Assert.Equal(ExecutableArchitecture.X64, result.ExecutableArchitecture);
    }

    [Fact]
    public void ReportsUnknownArchitectureForMultipleAmbiguousExecutables()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout(executableName: "a.exe", dataDirectoryName: "Game_Data");
        DiscoveryTestFixture.WriteMinimalPe(Path.Combine(fixture.Root, "b.exe"), 0x8664);

        var result = Inspect(fixture);

        Assert.Equal(ExecutableArchitecture.Unknown, result.ExecutableArchitecture);
        Assert.Contains(result.MissingOrConflictingEvidence, item => item.Contains("multiple", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SelectsTheOnlyTopLevelExecutableWhenNoNameMatchExists()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout(executableName: "only.exe", dataDirectoryName: "Game_Data");

        var result = Inspect(fixture);

        Assert.Contains(result.DetectedEvidence, item => item.RelativePath == "only.exe");
        Assert.Equal(ExecutableArchitecture.X64, result.ExecutableArchitecture);
    }

    [Fact]
    public void DoesNotSelectAMalformedOnlyExecutable()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout(executableName: "broken.exe", dataDirectoryName: "Game_Data");
        File.WriteAllText(Path.Combine(fixture.Root, "broken.exe"), "not a PE file");

        var result = Inspect(fixture);

        Assert.Equal(ExecutableArchitecture.Unknown, result.ExecutableArchitecture);
        Assert.DoesNotContain(result.DetectedEvidence, item => item.Category == "Selected executable");
    }

    [Fact]
    public void CliFailureDoesNotPrintTheSuppliedAbsoluteGamePath()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = DiscoveryCli.Run(
            ["inspect", "--game-path", fixture.Root, "--output-root", "invalid\0output"],
            stdout,
            stderr);

        Assert.Equal(2, exitCode);
        Assert.DoesNotContain(fixture.Root, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Root, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ", stderr.ToString(), StringComparison.Ordinal);
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
            : fixture.CreateExternalOutputRoot(outputName);
        return new DiscoveryEngine().Inspect(new DiscoveryRequest(
            fixture.Root,
            outputRoot,
            DiscoveryTimestampUtc: new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero)));
    }
}

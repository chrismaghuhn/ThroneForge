using System.Reflection;
using ThroneForge.Discovery;
using Xunit;

namespace ThroneForge.Discovery.Tests;

public sealed class RuntimeCompatibilityEngineTests
{
    private const string BaseFingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";

    [Fact]
    public void ClassifiesDirectNetstandard21Metadata()
    {
        var recommendation = RuntimeCompatibilityClassifier.Recommend(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("netstandard", new Version(2, 1), "netstandard2.1")],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Netstandard21Candidate, recommendation);
    }

    [Fact]
    public void ClassifiesNetstandard20FromUnity20211OrOlder()
    {
        var recommendation = RuntimeCompatibilityClassifier.Recommend(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("netstandard", new Version(2, 0), null)],
            "2021.1.28f1");

        Assert.Equal(TargetFrameworkRecommendation.Netstandard20Candidate, recommendation);
    }

    [Fact]
    public void ClassifiesNetstandard21FromUnity20212OrNewer()
    {
        var recommendation = RuntimeCompatibilityClassifier.Recommend(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("netstandard", new Version(2, 1), null)],
            "2021.2.0f1");

        Assert.Equal(TargetFrameworkRecommendation.Netstandard21Candidate, recommendation);
    }

    [Fact]
    public void DoesNotGuessExactNetstandardVersionWhenUnityVersionIsUnknown()
    {
        var recommendation = RuntimeCompatibilityClassifier.Recommend(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("netstandard", new Version(2, 0), null)],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.FrameworkCompatibleButExactTfmUnresolved, recommendation);
    }

    [Fact]
    public void UsesNet46CandidateForModernMscorlibWithoutNetstandard()
    {
        var recommendation = RuntimeCompatibilityClassifier.Recommend(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("mscorlib", new Version(4, 0), null)],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Net46Candidate, recommendation);
    }

    [Fact]
    public void UsesNet35FallbackForLegacyMscorlib()
    {
        var recommendation = RuntimeCompatibilityClassifier.Recommend(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("mscorlib", new Version(2, 0), null)],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Net35FallbackCandidate, recommendation);
    }

    [Fact]
    public void ReportsConflictingFrameworkEvidence()
    {
        var recommendation = RuntimeCompatibilityClassifier.Recommend(
            [
                RuntimeCompatibilityTestFixture.ManagedAssembly("netstandard", new Version(2, 0), "netstandard2.0"),
                RuntimeCompatibilityTestFixture.ManagedAssembly("mscorlib", new Version(2, 0), null)
            ],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Conflicting, recommendation);
    }

    [Fact]
    public void ReportsUnknownWhenFrameworkEvidenceIsMissing()
    {
        Assert.Equal(
            TargetFrameworkRecommendation.Unknown,
            RuntimeCompatibilityClassifier.Recommend([], "Unknown"));
    }

    [Fact]
    public void ReadsManagedMetadataWithoutLoadingTheAssembly()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        var destination = fixture.WriteCandidate("Game_Data/Managed/System.dll", File.ReadAllBytes(typeof(DiscoveryEngine).Assembly.Location));

        Assert.True(ManagedAssemblyInspector.TryInspect(
            destination,
            "Game_Data/Managed/System.dll",
            out var evidence));
        Assert.True(evidence.HasManagedMetadata);
        Assert.Equal("Game_Data/Managed/System.dll", evidence.RelativePath);
        Assert.NotNull(evidence.AssemblyName);
    }

    [Fact]
    public void ReportsMalformedManagedMetadataWithoutThrowing()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        var path = fixture.WriteCandidate("Game_Data/Managed/mscorlib.dll", [0x4D, 0x5A, 0x00]);

        Assert.False(ManagedAssemblyInspector.TryInspect(
            path,
            "Game_Data/Managed/mscorlib.dll",
            out var evidence));
        Assert.False(evidence.HasManagedMetadata);
        Assert.Contains("metadata", evidence.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OmitsOversizedManagedMetadataCandidate()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        var path = fixture.WriteCandidate("Game_Data/Managed/netstandard.dll", new byte[17 * 1024 * 1024]);

        Assert.False(ManagedAssemblyInspector.TryInspect(
            path,
            "Game_Data/Managed/netstandard.dll",
            out var evidence));
        Assert.Contains("size", evidence.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindsUnityVersionNearBeginningOfGlobalManagers()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        fixture.WriteCandidate("Game_Data/globalgamemanagers", System.Text.Encoding.UTF8.GetBytes("Unity 2022.3.12f1\0"));

        var result = fixture.Inspect(BaseFingerprint);

        Assert.Equal("2022.3.12f1", result.UnityVersion);
        Assert.Contains(result.UnityVersionEvidence, item => item.Source == "globalgamemanagers");
    }

    [Fact]
    public void DoesNotReadUnityVersionBeyondBoundedGlobalManagersPrefix()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        fixture.WriteCandidate(
            "Game_Data/globalgamemanagers",
            new byte[(256 * 1024) + 32]
                .Select((value, index) => index == (256 * 1024) + 4 ? (byte)'2' : value)
                .ToArray());

        var result = fixture.Inspect(BaseFingerprint);

        Assert.Equal("Unknown", result.UnityVersion);
        Assert.Contains(result.MissingOrConflictingEvidence, item => item.Contains("read limit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReportsConflictingUnityVersionSources()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        fixture.WriteCandidate("Game_Data/UnityVersion.txt", "2022.3.12f1");
        fixture.WriteCandidate("Game_Data/globalgamemanagers", System.Text.Encoding.UTF8.GetBytes("Unity 2021.1.28f1\0"));

        var result = fixture.Inspect(BaseFingerprint);

        Assert.Equal("Conflicting", result.UnityVersion);
        Assert.Contains(result.MissingOrConflictingEvidence, item => item.Contains("conflicting Unity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IncludesExecutableAndUnityPlayerVersionResourceEvidence()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        fixture.WriteCandidate("UnityPlayer.dll", [1, 2, 3]);

        var result = fixture.Inspect(
            BaseFingerprint,
            versionResourceReader: path => path.EndsWith("Thronefall.exe", StringComparison.OrdinalIgnoreCase)
                ? "2022.3.12f1"
                : path.EndsWith("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase)
                    ? "2022.3.12f1"
                    : null);

        Assert.Contains(result.UnityVersionEvidence, item => item.Source == "executable version resource");
        Assert.Contains(result.UnityVersionEvidence, item => item.Source == "UnityPlayer.dll version resource");
    }

    [Fact]
    public void NormalizesUnityVersionResourceBuildNumbersBeforeConflictChecking()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        fixture.WriteCandidate("Game_Data/globalgamemanagers", System.Text.Encoding.UTF8.GetBytes("Unity 2022.3.62f2\0"));
        fixture.WriteCandidate("UnityPlayer.dll", [1, 2, 3]);

        var result = fixture.Inspect(
            BaseFingerprint,
            versionResourceReader: path => path.EndsWith("Thronefall.exe", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase)
                ? "2022.3.62.7762112"
                : null);

        Assert.Equal("2022.3.62f2", result.UnityVersion);
        Assert.DoesNotContain(result.MissingOrConflictingEvidence, item =>
            item.Contains("Conflicting Unity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InventoriesAbsentLoaderIndicators()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();

        var result = fixture.Inspect(BaseFingerprint);

        Assert.All(result.LoaderIndicators, item => Assert.Equal(LoaderIndicatorStatus.Absent, item.Status));
    }

    [Fact]
    public void RecordsManagedRuntimeLayoutIndicatorsWithoutLoadingThem()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();

        var result = fixture.Inspect(BaseFingerprint);

        Assert.Contains(result.RuntimeLayoutEvidence, item =>
            item.RelativePath == "Game_Data/Managed" && item.IsDirectory && item.Present);
        Assert.Contains(result.RuntimeLayoutEvidence, item =>
            item.RelativePath == "Game_Data/Managed/Assembly-CSharp.dll" && !item.IsDirectory && item.Present);
        Assert.Contains(result.RuntimeLayoutEvidence, item =>
            item.RelativePath == "Game_Data/MonoBleedingEdge" && item.IsDirectory && item.Present);
        Assert.Contains("Managed-runtime layout evidence", result.ReportMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsDoorstopBepInExAndMelonLoaderIndicatorsWithoutExecutingThem()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        fixture.CreateDirectory("BepInEx");
        fixture.WriteCandidate("doorstop_config.ini", "# synthetic");
        fixture.CreateDirectory("MelonLoader");
        fixture.WriteCandidate("winhttp.dll", [1, 2, 3]);

        var result = fixture.Inspect(BaseFingerprint);

        Assert.Equal(LoaderIndicatorStatus.PotentialConflict, result.LoaderIndicators.Single(item => item.Name == "BepInEx/").Status);
        Assert.Equal(LoaderIndicatorStatus.PotentialConflict, result.LoaderIndicators.Single(item => item.Name == "doorstop_config.ini").Status);
        Assert.Equal(LoaderIndicatorStatus.PotentialConflict, result.LoaderIndicators.Single(item => item.Name == "MelonLoader/").Status);
        Assert.Equal(LoaderIndicatorStatus.Ambiguous, result.LoaderIndicators.Single(item => item.Name == "winhttp.dll").Status);
    }

    [Fact]
    public void RejectsOutputInsideGameRootBeforeCreatingAnything()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();

        Assert.Throws<DiscoveryException>(() => new RuntimeCompatibilityEngine().Inspect(new RuntimeCompatibilityRequest(
            fixture.Root,
            BaseFingerprint,
            Path.Combine(fixture.Root, "reports"))));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "reports")));
    }

    [Fact]
    public void RuntimeCompatibilityReportIsDeterministicAndSanitized()
    {
        using var first = new RuntimeCompatibilityTestFixture();
        using var second = new RuntimeCompatibilityTestFixture();
        first.CreateMonoLayout();
        second.CreateMonoLayout();
        var timestamp = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

        var firstResult = first.Inspect(BaseFingerprint, timestamp);
        var secondResult = second.Inspect(BaseFingerprint, timestamp);

        Assert.Equal(firstResult.ReportMarkdown, secondResult.ReportMarkdown);
        Assert.DoesNotContain(first.Root, firstResult.ReportMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.UserName, firstResult.ReportMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("# Thronefall Runtime Compatibility Report", firstResult.ReportMarkdown, StringComparison.Ordinal);
        Assert.Contains("Security and privacy statement", firstResult.ReportMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingRuntimeReportRequiresExplicitOverwrite()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        var first = fixture.Inspect(BaseFingerprint);

        var exception = Assert.Throws<DiscoveryException>(() => fixture.Inspect(BaseFingerprint));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.ReportMarkdown, File.ReadAllText(first.ReportPath));
        Assert.Empty(Directory.GetFiles(fixture.OutputRoot, "*.tmp"));
    }

    [Fact]
    public void RuntimeCompatibilityCliFailureDoesNotPrintTheSuppliedAbsoluteGamePath()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = DiscoveryCli.Run(
            [
                "runtime-compatibility",
                "--game-path",
                fixture.Root,
                "--fingerprint",
                BaseFingerprint,
                "--output-root",
                "invalid\0output"
            ],
            stdout,
            stderr);

        Assert.Equal(2, exitCode);
        Assert.DoesNotContain(fixture.Root, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Root, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCompatibilityCliWritesReportWithoutPrintingAbsolutePaths()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = DiscoveryCli.Run(
            [
                "runtime-compatibility",
                "--game-path",
                fixture.Root,
                "--fingerprint",
                BaseFingerprint,
                "--output-root",
                fixture.OutputRoot
            ],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr.ToString());
        Assert.DoesNotContain(fixture.Root, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(fixture.OutputRoot, $"{BaseFingerprint}-runtime-compatibility.md")));
    }
}

using ThroneForge.Discovery;
using Xunit;

namespace ThroneForge.Discovery.Tests;

public sealed class RuntimeCompatibilityHardeningTests
{
    [Fact]
    public void CleanSupportedProfileIsReadyForReversibleTest()
    {
        var readiness = RuntimeCompatibilityReadiness.Assess(
            ManagedRuntimeProfile.Mono,
            ExecutableArchitecture.X64,
            new TargetFrameworkAssessment(
                TargetFrameworkRecommendation.Netstandard21Candidate,
                TargetFrameworkConfidence.Medium,
                "netstandard compatibility surface plus Unity 2022.3 evidence"),
            "2022.3.62f2",
            []);

        Assert.Equal(SmokeTestReadiness.ReadyForReversibleTest, readiness.Status);
        Assert.Empty(readiness.BlockingIndicators);
    }

    [Theory]
    [InlineData("BepInEx", LoaderIndicatorStatus.PotentialConflict)]
    [InlineData("MelonLoader", LoaderIndicatorStatus.PotentialConflict)]
    [InlineData("doorstop_config.ini", LoaderIndicatorStatus.PotentialConflict)]
    [InlineData("winhttp.dll", LoaderIndicatorStatus.Ambiguous)]
    [InlineData("version.dll", LoaderIndicatorStatus.Ambiguous)]
    [InlineData("Mods", LoaderIndicatorStatus.Ambiguous)]
    [InlineData("Plugins", LoaderIndicatorStatus.Ambiguous)]
    public void AnyLoaderIndicatorBlocksCleanProfileReadiness(string name, LoaderIndicatorStatus status)
    {
        var readiness = RuntimeCompatibilityReadiness.Assess(
            ManagedRuntimeProfile.Mono,
            ExecutableArchitecture.X64,
            SupportedTargetFrameworkAssessment(),
            "2022.3.62f2",
            [new LoaderIndicatorEvidence(name, name, status, "synthetic indicator")]);

        Assert.Equal(SmokeTestReadiness.BlockedByExistingLoaderIndicators, readiness.Status);
        Assert.Contains(name, string.Join("; ", readiness.BlockingIndicators), StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingProfileBlocksReadiness()
    {
        var readiness = RuntimeCompatibilityReadiness.Assess(
            ManagedRuntimeProfile.Conflicting,
            ExecutableArchitecture.X64,
            SupportedTargetFrameworkAssessment(),
            "Conflicting",
            []);

        Assert.Equal(SmokeTestReadiness.BlockedByConflictingCompatibilityEvidence, readiness.Status);
    }

    [Fact]
    public void ConflictingFrameworkEvidenceBlocksReadiness()
    {
        var readiness = RuntimeCompatibilityReadiness.Assess(
            ManagedRuntimeProfile.Mono,
            ExecutableArchitecture.X64,
            new TargetFrameworkAssessment(
                TargetFrameworkRecommendation.Conflicting,
                TargetFrameworkConfidence.None,
                "Conflicting framework evidence"),
            "2022.3.62f2",
            []);

        Assert.Equal(SmokeTestReadiness.BlockedByConflictingCompatibilityEvidence, readiness.Status);
    }

    [Fact]
    public void UnknownArchitectureIsUnsupported()
    {
        var readiness = RuntimeCompatibilityReadiness.Assess(
            ManagedRuntimeProfile.Mono,
            ExecutableArchitecture.Unknown,
            SupportedTargetFrameworkAssessment(),
            "2022.3.62f2",
            []);

        Assert.Equal(SmokeTestReadiness.Unsupported, readiness.Status);
    }

    [Fact]
    public void UnknownFrameworkIsUnsupported()
    {
        var readiness = RuntimeCompatibilityReadiness.Assess(
            ManagedRuntimeProfile.Mono,
            ExecutableArchitecture.X64,
            new TargetFrameworkAssessment(
                TargetFrameworkRecommendation.Unknown,
                TargetFrameworkConfidence.None,
                "No compatible framework evidence"),
            "2022.3.62f2",
            []);

        Assert.Equal(SmokeTestReadiness.Unsupported, readiness.Status);
    }

    [Fact]
    public void CandidateSelectionRemainsIndependentFromReadiness()
    {
        var assessment = SupportedTargetFrameworkAssessment();
        var candidate = RuntimeCompatibilityClassifier.RecommendedCandidate(
            ManagedRuntimeProfile.Mono,
            ExecutableArchitecture.X64,
            assessment);
        var readiness = RuntimeCompatibilityReadiness.Assess(
            ManagedRuntimeProfile.Mono,
            ExecutableArchitecture.X64,
            assessment,
            "2022.3.62f2",
            [new LoaderIndicatorEvidence("BepInEx", "BepInEx", LoaderIndicatorStatus.PotentialConflict, "synthetic indicator")]);

        Assert.Equal("BepInEx 5 Unity Mono x64 5.4.23.5", candidate);
        Assert.Equal(SmokeTestReadiness.BlockedByExistingLoaderIndicators, readiness.Status);
    }

    [Fact]
    public void DirectTargetFrameworkEvidenceHasHighConfidence()
    {
        var assessment = RuntimeCompatibilityClassifier.Assess(
            [RuntimeCompatibilityTestFixture.ManagedAssembly(
                "netstandard",
                new Version(2, 1),
                ".NETStandard,Version=v2.1")],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Netstandard21Candidate, assessment.Recommendation);
        Assert.Equal(TargetFrameworkConfidence.High, assessment.Confidence);
        Assert.Contains("TargetFrameworkAttribute", assessment.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void UnityVersionInferenceHasMediumConfidence()
    {
        var assessment = RuntimeCompatibilityClassifier.Assess(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("netstandard", new Version(2, 1), null)],
            "2022.3.62f2");

        Assert.Equal(TargetFrameworkRecommendation.Netstandard21Candidate, assessment.Recommendation);
        Assert.Equal(TargetFrameworkConfidence.Medium, assessment.Confidence);
        Assert.Equal("netstandard compatibility surface plus Unity 2022.3 evidence", assessment.Basis);
    }

    [Fact]
    public void LegacyFallbackHasLowConfidence()
    {
        var assessment = RuntimeCompatibilityClassifier.Assess(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("mscorlib", new Version(2, 0), null)],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Net35FallbackCandidate, assessment.Recommendation);
        Assert.Equal(TargetFrameworkConfidence.Low, assessment.Confidence);
    }

    [Fact]
    public void ModernMscorlibInferenceHasMediumConfidence()
    {
        var assessment = RuntimeCompatibilityClassifier.Assess(
            [RuntimeCompatibilityTestFixture.ManagedAssembly("mscorlib", new Version(4, 0), null)],
            "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Net46Candidate, assessment.Recommendation);
        Assert.Equal(TargetFrameworkConfidence.Medium, assessment.Confidence);
    }

    [Fact]
    public void UnresolvedFrameworkHasNoConfidence()
    {
        var assessment = RuntimeCompatibilityClassifier.Assess([], "Unknown");

        Assert.Equal(TargetFrameworkRecommendation.Unknown, assessment.Recommendation);
        Assert.Equal(TargetFrameworkConfidence.None, assessment.Confidence);
    }

    private static TargetFrameworkAssessment SupportedTargetFrameworkAssessment()
        => new(
            TargetFrameworkRecommendation.Netstandard21Candidate,
            TargetFrameworkConfidence.Medium,
            "netstandard compatibility surface plus Unity 2022.3 evidence");
}

using ThroneForge.Discovery;
using Xunit;

namespace ThroneForge.Discovery.Tests;

public sealed class RuntimeCompatibilityEvidenceContractTests
{
    private const string Fingerprint = "1ddd8982e790969cb208cf91bb1489123413d167f9e07cd0416ab6739d4fcd7d";

    [Fact]
    public void ParsesStrictMachineReadableEvidence()
    {
        var evidence = RuntimeCompatibilityEvidenceContract.Parse(Json(
            selected: "Thronefall.exe",
            readiness: "ReadyForReversibleTest"));

        Assert.Equal(RuntimeCompatibilityEvidenceContract.SchemaVersion, evidence.SchemaVersion);
        Assert.Equal(Fingerprint, evidence.GameFingerprint);
        Assert.True(evidence.IsReadyForReversibleTest);
        Assert.True(evidence.LoaderIndicatorsAbsent);
    }

    [Fact]
    public void HumanColonPresentationIsNotAccepted()
    {
        Assert.Throws<DiscoveryException>(() => RuntimeCompatibilityEvidenceContract.Parse(
            "Selected executable: Thronefall.exe"));
    }

    [Fact]
    public void MissingSelectedExecutableFailsClosed()
    {
        Assert.Throws<DiscoveryException>(() => RuntimeCompatibilityEvidenceContract.Parse(
            Json(selected: null, readiness: "ReadyForReversibleTest")));
    }

    [Fact]
    public void WrongFingerprintFailsClosed()
    {
        Assert.Throws<DiscoveryException>(() => RuntimeCompatibilityEvidenceContract.Parse(
            Json(fingerprint: new string('a', 64), selected: "Thronefall.exe", readiness: "ReadyForReversibleTest"), Fingerprint));
    }

    [Fact]
    public void NonReadyEvidenceIsRepresentedWithoutBeingAccepted()
    {
        var evidence = RuntimeCompatibilityEvidenceContract.Parse(Json(
            selected: "Thronefall.exe",
            readiness: "BlockedByExistingLoaderIndicators",
            loaderIndicatorsAbsent: false));

        Assert.False(evidence.IsReadyForReversibleTest);
        Assert.False(evidence.LoaderIndicatorsAbsent);
    }

    [Fact]
    public void DuplicateFieldsFailClosed()
    {
        var duplicate = Json("Thronefall.exe", "ReadyForReversibleTest")
            .Replace("\"schema-version\":\"throneforge-runtime-compatibility-evidence-v1\",", "\"schema-version\":\"throneforge-runtime-compatibility-evidence-v1\",\"schema-version\":\"throneforge-runtime-compatibility-evidence-v1\",", StringComparison.Ordinal);

        Assert.Throws<DiscoveryException>(() => RuntimeCompatibilityEvidenceContract.Parse(duplicate));
    }

    [Fact]
    public void AbsoluteSelectedExecutableFailsClosed()
    {
        Assert.Throws<DiscoveryException>(() => RuntimeCompatibilityEvidenceContract.Parse(
            Json(selected: "C:/private/Thronefall.exe", readiness: "ReadyForReversibleTest")));
    }

    [Fact]
    public void DiscoveryCliEmitsMachineContractWithoutHumanPresentation()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        using var output = new StringWriter();
        using var errors = new StringWriter();

        var exitCode = DiscoveryCli.Run(
            [
                "runtime-compatibility-evidence",
                "--game-path", fixture.Root,
                "--fingerprint", fixture.Fingerprint,
                "--output-root", fixture.OutputRoot,
                "--overwrite"
            ],
            output,
            errors);

        Assert.Equal(0, exitCode);
        var evidence = RuntimeCompatibilityEvidenceContract.Parse(output.ToString(), fixture.Fingerprint);
        Assert.DoesNotContain("Selected executable:", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFullPath(fixture.Root), output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(evidence.SelectedExecutableRelativePath));
    }

    private static string Json(
        string? selected = "Thronefall.exe",
        string readiness = "ReadyForReversibleTest",
        string fingerprint = Fingerprint,
        bool loaderIndicatorsAbsent = true)
        => $$"""
        {"schema-version":"throneforge-runtime-compatibility-evidence-v1","game-fingerprint":"{{fingerprint}}","selected-executable-relative-path":{{(selected is null ? "null" : $"\"{selected}\"")}},"managed-runtime-profile":"Mono","executable-architecture":"X64","unity-version":"2022.3.62f2","smoke-test-readiness":"{{readiness}}","loader-indicators-absent":{{loaderIndicatorsAbsent.ToString().ToLowerInvariant()}}}
        """;
}

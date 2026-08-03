using ThroneForge.Discovery;
using Xunit;

namespace ThroneForge.Discovery.Tests;

public sealed class RuntimeCompatibilityFingerprintBindingTests
{
    [Fact]
    public void MatchingInstallationAndFingerprintSucceeds()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();

        var result = fixture.Inspect(fixture.Fingerprint);

        Assert.Equal(fixture.Fingerprint, result.BaseFingerprint);
        Assert.True(File.Exists(result.ReportPath));
    }

    [Fact]
    public void TaskOneReportUsesTheSameSharedFingerprintSnapshot()
    {
        using var fixture = new DiscoveryTestFixture();
        fixture.CreateMonoLayout();

        var snapshotFingerprint = InstallationFingerprintService.Capture(fixture.Root).Fingerprint;
        var taskOne = new DiscoveryEngine().Inspect(new DiscoveryRequest(fixture.Root, fixture.OutputRoot));

        Assert.Equal(snapshotFingerprint, taskOne.Fingerprint);
    }

    [Fact]
    public void SyntacticallyValidIncorrectFingerprintFailsBeforeWriting()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();

        var exception = Assert.Throws<DiscoveryException>(() => fixture.Inspect(new string('a', 64)));

        Assert.Contains("does not match", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(fixture.OutputRoot));
    }

    [Fact]
    public void ChangingSelectedAssemblyAfterTaskOneCausesMismatch()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        var fingerprint = fixture.Fingerprint;
        File.AppendAllText(Path.Combine(fixture.Root, "Game_Data", "Managed", "Assembly-CSharp.dll"), "changed");

        Assert.Throws<DiscoveryException>(() => fixture.Inspect(fingerprint));
    }

    [Fact]
    public void ChangingSelectedExecutableAfterTaskOneCausesMismatch()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        var fingerprint = fixture.Fingerprint;
        File.AppendAllText(Path.Combine(fixture.Root, "Thronefall.exe"), "changed");

        Assert.Throws<DiscoveryException>(() => fixture.Inspect(fingerprint));
    }

    [Fact]
    public void ChangingBackendEvidenceAfterTaskOneCausesMismatch()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        var fingerprint = fixture.Fingerprint;
        fixture.CreateDirectory("Game_Data/il2cpp_data/Metadata");
        fixture.WriteCandidate("Game_Data/il2cpp_data/Metadata/global-metadata.dat", [1, 2, 3]);
        RuntimeCompatibilityTestFixture.WriteMinimalPe(Path.Combine(fixture.Root, "GameAssembly.dll"), 0x8664);

        Assert.Throws<DiscoveryException>(() => fixture.Inspect(fingerprint));
    }

    [Fact]
    public void MismatchLeavesNoTemporaryFileOrTaskOneReportChange()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();
        var taskOneOutput = Path.Combine(Path.GetTempPath(), $"throneforge-task1-{Guid.NewGuid():N}");
        try
        {
            var taskOne = new DiscoveryEngine().Inspect(new DiscoveryRequest(fixture.Root, taskOneOutput));
            var originalTaskOneReport = File.ReadAllText(taskOne.ReportPath);
            File.AppendAllText(Path.Combine(fixture.Root, "Game_Data", "Managed", "Assembly-CSharp.dll"), "changed");

            Assert.Throws<DiscoveryException>(() => fixture.Inspect(taskOne.Fingerprint));
            Assert.Equal(originalTaskOneReport, File.ReadAllText(taskOne.ReportPath));
            Assert.False(Directory.Exists(fixture.OutputRoot));
            Assert.Empty(Directory.GetFiles(taskOneOutput, "*.tmp"));
            Assert.Empty(Directory.GetFiles(fixture.Root, "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(taskOneOutput))
            {
                Directory.Delete(taskOneOutput, recursive: true);
            }
        }
    }

    [Fact]
    public void EquivalentUppercaseFingerprintIsAccepted()
    {
        using var fixture = new RuntimeCompatibilityTestFixture();
        fixture.CreateMonoLayout();

        var result = fixture.Inspect(fixture.Fingerprint.ToUpperInvariant());

        Assert.Equal(fixture.Fingerprint.ToUpperInvariant(), result.BaseFingerprint);
    }

    [Fact]
    public void FingerprintV1RemainsDeterministicForTheSyntheticFixture()
    {
        using var first = new DiscoveryTestFixture();
        using var second = new DiscoveryTestFixture();
        first.CreateMonoLayout();
        second.CreateMonoLayout();

        var firstFingerprint = InstallationFingerprintService.Capture(first.Root).Fingerprint;
        var secondFingerprint = InstallationFingerprintService.Capture(second.Root).Fingerprint;

        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.Equal("0d9c25d94ab3ad91b766c4e1e0b6777efb8906079896bba95856e19fb4715420", firstFingerprint);
    }

    [Fact]
    public void FingerprintV1RemainsDeterministicForTheSyntheticIl2CppFixture()
    {
        using var first = new DiscoveryTestFixture();
        using var second = new DiscoveryTestFixture();
        first.CreateIl2CppLayout();
        second.CreateIl2CppLayout();

        Assert.Equal(
            InstallationFingerprintService.Capture(first.Root).Fingerprint,
            InstallationFingerprintService.Capture(second.Root).Fingerprint);
    }

    [Fact]
    public void CliMismatchDoesNotPrintAbsoluteGamePath()
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
                new string('b', 64),
                "--output-root",
                fixture.OutputRoot
            ],
            stdout,
            stderr);

        Assert.Equal(2, exitCode);
        Assert.DoesNotContain(fixture.Root, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.Root, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ", stderr.ToString(), StringComparison.Ordinal);
    }
}

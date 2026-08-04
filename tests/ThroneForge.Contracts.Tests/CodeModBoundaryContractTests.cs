using System.Reflection;
using ThroneForge.API;
using ThroneForge.Contracts;
using Xunit;

namespace ThroneForge.Contracts.Tests;

public sealed class CodeModBoundaryContractTests
{
    private const string PackageHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string GameFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void DescriptorNormalizesPackageHashWithoutStoringAPath()
    {
        var descriptor = new CodeModDescriptor(
            new ModIdentity("dev.example.mod", "1.0.0"),
            PackageHash.ToUpperInvariant());

        Assert.Equal(PackageHash, descriptor.PackageSha256.Value);
        Assert.True(descriptor.HasValidPackageSha256);
        Assert.DoesNotContain(
            typeof(CodeModDescriptor).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == typeof(string)
                && property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha256")]
    [InlineData("0123456789abcdef")]
    public void MalformedSha256CannotBecomeAValue(string hash)
    {
        Assert.Throws<ArgumentException>(() => new Sha256Digest(hash));
    }

    [Fact]
    public void ModIdentityIsCanonicalAndCaseInsensitive()
    {
        var identity = new ModIdentity("  DEV.Example.Mod  ", " 1.0.0 ");

        Assert.Equal("dev.example.mod", identity.Id);
        Assert.Equal("1.0.0", identity.Version);
        Assert.Equal(identity, new ModIdentity("dev.example.mod", "1.0.0"));
    }

    [Theory]
    [InlineData("dev/example.mod")]
    [InlineData("dev\\example.mod")]
    [InlineData("dev:example.mod")]
    [InlineData("dev..example.mod")]
    [InlineData("dev. example.mod")]
    [InlineData("dev\u0001.example.mod")]
    [InlineData(".dev.example.mod")]
    [InlineData("dev.example.mod.")]
    public void UnsafeModIdsAreRejected(string id)
    {
        Assert.Throws<ArgumentException>(() => new ModIdentity(id, "1.0.0"));
    }

    [Fact]
    public void ExcessivelyLongModIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ModIdentity(new string('a', 65), "1.0.0"));
    }

    [Theory]
    [InlineData("anything")]
    [InlineData("1.0")]
    [InlineData("1.0.0/path")]
    [InlineData("1.0.0 beta")]
    public void InvalidVersionsAreRejected(string version)
    {
        Assert.Throws<ArgumentException>(() => new ModIdentity("dev.example.mod", version));
    }

    [Fact]
    public void ExcessivelyLongVersionIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ModIdentity("dev.example.mod", new string('1', 65)));
    }

    [Fact]
    public void IntegrityEvidenceBindsIdentityAndBothHashes()
    {
        var identity = new ModIdentity("dev.example.mod", "1.0.0");
        var evidence = new CodeModIntegrityEvidence(
            identity,
            new Sha256Digest(PackageHash),
            new Sha256Digest(PackageHash.ToUpperInvariant()),
            CodeModIntegrityVerificationStatus.Verified,
            "sha256");

        Assert.Equal(identity, evidence.ModIdentity);
        Assert.Equal(new Sha256Digest(PackageHash), evidence.ExpectedPackageSha256);
        Assert.Equal(evidence.ExpectedPackageSha256, evidence.ObservedPackageSha256);
        Assert.Equal(CodeModIntegrityVerificationStatus.Verified, evidence.Status);
    }

    [Fact]
    public void VerificationMethodCannotContainAPath()
    {
        var identity = new ModIdentity("dev.example.mod", "1.0.0");

        Assert.Throws<ArgumentException>(() => new CodeModIntegrityEvidence(
            identity,
            new Sha256Digest(PackageHash),
            new Sha256Digest(PackageHash),
            CodeModIntegrityVerificationStatus.Verified,
            "C:/verification.log"));
    }

    [Fact]
    public void ApprovalBindsExactPackageAndGameBuild()
    {
        var approval = new CodeModApprovalRecord(
            new ModIdentity("dev.example.mod", "1.0.0"),
            new Sha256Digest(PackageHash),
            new GameFingerprint(GameFingerprint),
            CodeModApprovalDecision.Approved,
            CodeModApprovalScope.ExactPackageAndGameBuild,
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(CodeModApprovalDecision.Approved, approval.Decision);
        Assert.Equal(CodeModApprovalScope.ExactPackageAndGameBuild, approval.Scope);
        Assert.Equal(TimeSpan.Zero, approval.RecordedAtUtc.Offset);
        Assert.Equal(GameFingerprint, approval.GameFingerprint.Value);
    }

    [Fact]
    public void AdapterEvidenceCarriesOnlyCanonicalPortableFacts()
    {
        var evidence = new AdapterCompatibilityEvidence(
            new GameFingerprint(GameFingerprint.ToUpperInvariant()),
            "Thronefall.Adapter",
            "1.0.0",
            AdapterCompatibility.Supported);

        Assert.Equal(GameFingerprint, evidence.GameFingerprint.Value);
        Assert.Equal("thronefall.adapter", evidence.AdapterId);
        Assert.Equal("1.0.0", evidence.AdapterVersion);
    }

    [Fact]
    public void ActivationRequestContainsBoundEvidenceInsteadOfIndependentFlags()
    {
        var identity = new ModIdentity("dev.example.mod", "1.0.0");
        var descriptor = new CodeModDescriptor(identity, PackageHash);
        var integrity = new CodeModIntegrityEvidence(
            identity,
            descriptor.PackageSha256,
            descriptor.PackageSha256,
            CodeModIntegrityVerificationStatus.Verified,
            "sha256");
        var compatibility = new AdapterCompatibilityEvidence(
            new GameFingerprint(GameFingerprint),
            "thronefall.adapter",
            "1.0.0",
            AdapterCompatibility.Supported);
        var request = new CodeModActivationRequest(
            descriptor,
            new GameFingerprint(GameFingerprint),
            integrity,
            approval: null,
            compatibility);

        Assert.Equal(descriptor, request.Descriptor);
        Assert.Equal(integrity, request.IntegrityEvidence);
        Assert.Null(request.Approval);
        Assert.Equal(compatibility, request.CompatibilityEvidence);
        Assert.DoesNotContain(
            typeof(CodeModActivationRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "PackageIntegrityVerified" or "ExplicitApproval");
    }

    [Fact]
    public void AdmissionBindingDigestIsDeterministicAndPathFree()
    {
        var identity = new ModIdentity("dev.example.mod", "1.0.0");
        var first = new CodeModAdmissionBinding(
            identity,
            new Sha256Digest(PackageHash),
            new GameFingerprint(GameFingerprint),
            "thronefall.adapter",
            "1.0.0");
        var second = new CodeModAdmissionBinding(
            identity,
            new Sha256Digest(PackageHash),
            new GameFingerprint(GameFingerprint),
            "thronefall.adapter",
            "1.0.0");

        Assert.Equal(first.BindingDigest, second.BindingDigest);
        Assert.Equal(64, first.BindingDigest.Length);
        Assert.DoesNotContain("/", first.BindingDigest, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", first.BindingDigest, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicModContractUsesPortableLifecycleOnly()
    {
        var initialize = typeof(IThroneForgeMod).GetMethod(nameof(IThroneForgeMod.InitializeAsync));
        var shutdown = typeof(IThroneForgeMod).GetMethod(nameof(IThroneForgeMod.ShutdownAsync));

        Assert.NotNull(initialize);
        Assert.NotNull(shutdown);
        Assert.Equal(typeof(ValueTask), initialize!.ReturnType);
        Assert.Equal(typeof(ValueTask), shutdown!.ReturnType);
        Assert.DoesNotContain(
            typeof(IThroneForgeMod).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "UnityEngine" or "BepInEx" or "HarmonyLib" or "Assembly-CSharp");
    }
}

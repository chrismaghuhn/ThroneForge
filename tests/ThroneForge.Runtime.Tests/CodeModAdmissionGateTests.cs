using ThroneForge.Contracts;
using ThroneForge.Runtime;
using Xunit;

namespace ThroneForge.Runtime.Tests;

public sealed class CodeModAdmissionGateTests
{
    private const string PackageHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string OtherPackageHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    private const string GameFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OtherGameFingerprint = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void ApprovalForAnotherPackageHashIsRejected()
    {
        var request = CreateRequest(approvalHash: OtherPackageHash);

        var decision = CodeModAdmissionGate.Evaluate(request);

        AssertRejected(decision, CodeModAdmissionReasonCodes.ApprovalMismatch);
    }

    [Fact]
    public void ApprovalForAnotherModIdentityIsRejected()
    {
        var request = CreateRequest(approvalIdentity: new ModIdentity("other.example.mod", "1.0.0"));

        var decision = CodeModAdmissionGate.Evaluate(request);

        AssertRejected(decision, CodeModAdmissionReasonCodes.ApprovalMismatch);
    }

    [Fact]
    public void ApprovalForAnotherGameFingerprintIsRejected()
    {
        var request = CreateRequest(approvalFingerprint: new GameFingerprint(OtherGameFingerprint));

        var decision = CodeModAdmissionGate.Evaluate(request);

        AssertRejected(decision, CodeModAdmissionReasonCodes.ApprovalMismatch);
    }

    [Fact]
    public void DeniedApprovalIsRejected()
    {
        var request = CreateRequest(approvalDecision: CodeModApprovalDecision.Denied);

        var decision = CodeModAdmissionGate.Evaluate(request);

        AssertRejected(decision, CodeModAdmissionReasonCodes.ApprovalMismatch);
    }

    [Fact]
    public void MissingApprovalRequiresExplicitApprovalAndStillCarriesBinding()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(includeApproval: false));

        Assert.Equal(CodeModAdmissionStatus.RequiresExplicitApproval, decision.Status);
        Assert.Equal(CodeModAdmissionReasonCodes.ApprovalRequired, decision.ReasonCode);
        Assert.NotNull(decision.Binding);
    }

    [Fact]
    public void IntegrityEvidenceForAnotherPackageIsRejected()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(integrityExpectedHash: OtherPackageHash));

        AssertRejected(decision, CodeModAdmissionReasonCodes.IntegrityNotVerified);
    }

    [Fact]
    public void ObservedPackageHashMismatchIsRejected()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(integrityObservedHash: OtherPackageHash));

        AssertRejected(decision, CodeModAdmissionReasonCodes.IntegrityNotVerified);
    }

    [Fact]
    public void CompatibilityEvidenceForAnotherFingerprintIsRejected()
    {
        var decision = CodeModAdmissionGate.Evaluate(
            CreateRequest(compatibilityFingerprint: new GameFingerprint(OtherGameFingerprint)));

        AssertRejected(decision, CodeModAdmissionReasonCodes.CompatibilityFingerprintMismatch);
    }

    [Fact]
    public void CompatibilityEvidenceBindingAppearsInTheDecision()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest());

        Assert.Equal(CodeModAdmissionStatus.Approved, decision.Status);
        Assert.NotNull(decision.Binding);
        Assert.Equal("thronefall.adapter", decision.Binding!.AdapterId);
        Assert.Equal("1.0.0", decision.Binding.AdapterVersion);
        Assert.Equal(PackageHash, decision.Binding.PackageSha256.Value);
        Assert.Equal(GameFingerprint, decision.Binding.GameFingerprint.Value);
    }

    [Fact]
    public void SupportedWithWarningsIsRejected()
    {
        var decision = CodeModAdmissionGate.Evaluate(
            CreateRequest(compatibility: AdapterCompatibility.SupportedWithWarnings));

        AssertRejected(decision, CodeModAdmissionReasonCodes.CompatibilityUnsupported);
    }

    [Fact]
    public void CompatibilityFailurePrecedesAStaleApproval()
    {
        var decision = CodeModAdmissionGate.Evaluate(
            CreateRequest(
                compatibility: AdapterCompatibility.SupportedWithWarnings,
                approvalHash: OtherPackageHash));

        AssertRejected(decision, CodeModAdmissionReasonCodes.CompatibilityUnsupported);
    }

    [Fact]
    public void UnknownCompatibilityValueIsRejected()
    {
        var decision = CodeModAdmissionGate.Evaluate(
            CreateRequest(compatibility: (AdapterCompatibility)999));

        AssertRejected(decision, CodeModAdmissionReasonCodes.CompatibilityUnsupported);
    }

    [Fact]
    public void ApprovedResultContainsTheExactArtifactBinding()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest());

        Assert.Equal(CodeModAdmissionStatus.Approved, decision.Status);
        Assert.NotNull(decision.Binding);
        Assert.Equal(new ModIdentity("dev.example.mod", "1.0.0"), decision.Binding!.ModIdentity);
        Assert.Equal(PackageHash, decision.Binding.PackageSha256.Value);
    }

    [Fact]
    public void BindingDigestIsDeterministic()
    {
        var first = CodeModAdmissionGate.Evaluate(CreateRequest());
        var second = CodeModAdmissionGate.Evaluate(CreateRequest());

        Assert.Equal(first.Binding!.BindingDigest, second.Binding!.BindingDigest);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("package")]
    [InlineData("fingerprint")]
    [InlineData("adapter-id")]
    [InlineData("adapter-version")]
    public void ChangingAnyBindingFieldChangesTheDigest(string changedField)
    {
        var identity = new ModIdentity("dev.example.mod", "1.0.0");
        var binding = new CodeModAdmissionBinding(
            identity,
            new Sha256Digest(PackageHash),
            new GameFingerprint(GameFingerprint),
            "thronefall.adapter",
            "1.0.0");
        var changed = changedField switch
        {
            "identity" => new CodeModAdmissionBinding(new ModIdentity("other.example.mod", "1.0.0"), binding.PackageSha256, binding.GameFingerprint, binding.AdapterId, binding.AdapterVersion),
            "package" => new CodeModAdmissionBinding(binding.ModIdentity, new Sha256Digest(OtherPackageHash), binding.GameFingerprint, binding.AdapterId, binding.AdapterVersion),
            "fingerprint" => new CodeModAdmissionBinding(binding.ModIdentity, binding.PackageSha256, new GameFingerprint(OtherGameFingerprint), binding.AdapterId, binding.AdapterVersion),
            "adapter-id" => new CodeModAdmissionBinding(binding.ModIdentity, binding.PackageSha256, binding.GameFingerprint, "other.adapter", binding.AdapterVersion),
            "adapter-version" => new CodeModAdmissionBinding(binding.ModIdentity, binding.PackageSha256, binding.GameFingerprint, binding.AdapterId, "2.0.0"),
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };

        Assert.NotEqual(binding.BindingDigest, changed.BindingDigest);
    }

    [Fact]
    public void BindingAndEvidenceExposeNoFilesystemPathOrExecutableObject()
    {
        var request = CreateRequest();

        Assert.DoesNotContain(
            request.GetType().GetProperties(),
            property => property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            request.IntegrityEvidence.GetType().GetProperties(),
            property => property.PropertyType == typeof(Stream));
        Assert.DoesNotContain(
            request.CompatibilityEvidence.GetType().GetProperties(),
            property => property.PropertyType == typeof(System.Reflection.Assembly));
    }

    private static CodeModActivationRequest CreateRequest(
        AdapterCompatibility compatibility = AdapterCompatibility.Supported,
        CodeModApprovalDecision approvalDecision = CodeModApprovalDecision.Approved,
        CodeModApprovalRecord? approval = null,
        string? approvalHash = null,
        ModIdentity? approvalIdentity = null,
        GameFingerprint? approvalFingerprint = null,
        string? integrityExpectedHash = null,
        string? integrityObservedHash = null,
        GameFingerprint? compatibilityFingerprint = null,
        bool includeApproval = true)
    {
        var identity = new ModIdentity("dev.example.mod", "1.0.0");
        var descriptor = new CodeModDescriptor(identity, PackageHash);
        var integrity = new CodeModIntegrityEvidence(
            identity,
            new Sha256Digest(integrityExpectedHash ?? PackageHash),
            new Sha256Digest(integrityObservedHash ?? PackageHash),
            CodeModIntegrityVerificationStatus.Verified,
            "sha256");
        var gameFingerprint = new GameFingerprint(GameFingerprint);
        var compatibilityEvidence = new AdapterCompatibilityEvidence(
            compatibilityFingerprint ?? gameFingerprint,
            "thronefall.adapter",
            "1.0.0",
            compatibility);

        if (includeApproval)
        {
            approval ??= new CodeModApprovalRecord(
                approvalIdentity ?? identity,
                new Sha256Digest(approvalHash ?? PackageHash),
                approvalFingerprint ?? gameFingerprint,
                approvalDecision,
                CodeModApprovalScope.ExactPackageAndGameBuild,
                new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero));
        }
        else
        {
            approval = null;
        }

        return new CodeModActivationRequest(
            descriptor,
            gameFingerprint,
            integrity,
            approval,
            compatibilityEvidence);
    }

    private static void AssertRejected(CodeModAdmissionDecision decision, string reasonCode)
    {
        Assert.Equal(CodeModAdmissionStatus.Rejected, decision.Status);
        Assert.Equal(reasonCode, decision.ReasonCode);
    }
}

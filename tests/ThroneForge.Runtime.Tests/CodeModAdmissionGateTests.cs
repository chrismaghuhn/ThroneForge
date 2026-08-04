using ThroneForge.Contracts;
using ThroneForge.Runtime;
using Xunit;

namespace ThroneForge.Runtime.Tests;

public sealed class CodeModAdmissionGateTests
{
    private static readonly CodeModDescriptor Descriptor = new(
        new ModIdentity("dev.example.mod", "1.0.0"),
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789");

    [Fact]
    public void MalformedPackageHashIsRejected()
    {
        var request = new CodeModActivationRequest(
            new CodeModDescriptor(new ModIdentity("dev.example.mod", "1.0.0"), "not-a-sha256"),
            new GameFingerprint("game-fingerprint"),
            AdapterCompatibility.Supported,
            packageIntegrityVerified: true,
            explicitApproval: true);

        var decision = CodeModAdmissionGate.Evaluate(request);

        Assert.Equal(CodeModAdmissionStatus.Rejected, decision.Status);
        Assert.Equal("TF-RUN-009", decision.ReasonCode);
    }

    [Fact]
    public void UnverifiedPackageIntegrityIsRejectedBeforeApprovalIsConsidered()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(
            AdapterCompatibility.Supported,
            packageIntegrityVerified: false,
            explicitApproval: true));

        Assert.Equal(CodeModAdmissionStatus.Rejected, decision.Status);
        Assert.Equal("TF-RUN-010", decision.ReasonCode);
    }

    [Fact]
    public void UnsupportedAdapterCompatibilityIsRejected()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(
            AdapterCompatibility.UnknownBuild,
            packageIntegrityVerified: true,
            explicitApproval: true));

        Assert.Equal(CodeModAdmissionStatus.Rejected, decision.Status);
        Assert.Equal("TF-ADP-001", decision.ReasonCode);
    }

    [Fact]
    public void MissingExplicitApprovalRequiresUserDecision()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(
            AdapterCompatibility.Supported,
            packageIntegrityVerified: true,
            explicitApproval: false));

        Assert.Equal(CodeModAdmissionStatus.RequiresExplicitApproval, decision.Status);
        Assert.Equal("TF-RUN-011", decision.ReasonCode);
    }

    [Fact]
    public void VerifiedAndExplicitlyApprovedCodeModIsAdmittedWithoutLoadingIt()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(
            AdapterCompatibility.Supported,
            packageIntegrityVerified: true,
            explicitApproval: true));

        Assert.Equal(CodeModAdmissionStatus.Approved, decision.Status);
        Assert.Equal("TF-RUN-012", decision.ReasonCode);
        Assert.Contains("future loader", decision.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WarningCompatibilityDoesNotSilentlyAdmitFullTrustCode()
    {
        var decision = CodeModAdmissionGate.Evaluate(CreateRequest(
            AdapterCompatibility.SupportedWithWarnings,
            packageIntegrityVerified: true,
            explicitApproval: true));

        Assert.Equal(CodeModAdmissionStatus.Rejected, decision.Status);
        Assert.Equal("TF-ADP-001", decision.ReasonCode);
    }

    private static CodeModActivationRequest CreateRequest(
        AdapterCompatibility compatibility,
        bool packageIntegrityVerified,
        bool explicitApproval) =>
        new(
            Descriptor,
            new GameFingerprint("game-fingerprint"),
            compatibility,
            packageIntegrityVerified,
            explicitApproval);
}

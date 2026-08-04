using ThroneForge.Contracts;

namespace ThroneForge.Runtime;

public enum CodeModAdmissionStatus
{
    Rejected = 0,
    RequiresExplicitApproval,
    Approved
}

public static class CodeModAdmissionReasonCodes
{
    public const string InvalidRequest = "TF-RUN-008";
    public const string InvalidPackageHash = "TF-RUN-009";
    public const string IntegrityNotVerified = "TF-RUN-010";
    public const string ApprovalRequired = "TF-RUN-011";
    public const string Approved = "TF-RUN-012";
    public const string ApprovalMismatch = "TF-RUN-013";
    public const string CompatibilityUnsupported = "TF-ADP-001";
    public const string CompatibilityFingerprintMismatch = "TF-ADP-003";
}

public sealed record CodeModAdmissionDecision(
    CodeModAdmissionStatus Status,
    string ReasonCode,
    string Message,
    CodeModAdmissionBinding? Binding);

public static class CodeModAdmissionGate
{
    public static CodeModAdmissionDecision Evaluate(CodeModActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HasValidRequestShape(request))
        {
            return Rejected(
                CodeModAdmissionReasonCodes.InvalidRequest,
                "The code-mod admission request is malformed or internally contradictory.",
                binding: null);
        }

        if (!request.Descriptor.HasValidPackageSha256)
        {
            return Rejected(
                CodeModAdmissionReasonCodes.InvalidPackageHash,
                "The code-mod package SHA-256 is not a valid 64-character hexadecimal digest.",
                binding: null);
        }

        if (request.IntegrityEvidence.Status != CodeModIntegrityVerificationStatus.Verified
            || request.IntegrityEvidence.ModIdentity != request.Descriptor.Identity
            || request.IntegrityEvidence.ExpectedPackageSha256 != request.Descriptor.PackageSha256
            || request.IntegrityEvidence.ObservedPackageSha256 != request.IntegrityEvidence.ExpectedPackageSha256)
        {
            return Rejected(
                CodeModAdmissionReasonCodes.IntegrityNotVerified,
                "The code-mod package integrity evidence is missing, failed, or bound to another artifact.",
                binding: null);
        }

        if (!Enum.IsDefined(request.CompatibilityEvidence.Compatibility)
            || request.CompatibilityEvidence.Compatibility != AdapterCompatibility.Supported)
        {
            return Rejected(
                CodeModAdmissionReasonCodes.CompatibilityUnsupported,
                "The compatibility evidence does not prove an unsupported or warning-free supported adapter state.",
                binding: null);
        }

        if (!string.Equals(
                request.GameFingerprint.Value,
                request.CompatibilityEvidence.GameFingerprint.Value,
                StringComparison.Ordinal))
        {
            return Rejected(
                CodeModAdmissionReasonCodes.CompatibilityFingerprintMismatch,
                "The compatibility evidence is bound to a different game fingerprint.",
                binding: null);
        }

        var binding = new CodeModAdmissionBinding(
            request.Descriptor.Identity,
            request.Descriptor.PackageSha256,
            request.GameFingerprint,
            request.CompatibilityEvidence.AdapterId,
            request.CompatibilityEvidence.AdapterVersion);

        if (request.Approval is not null && !MatchesApprovedBinding(request, binding))
        {
            return Rejected(
                CodeModAdmissionReasonCodes.ApprovalMismatch,
                "The approval is denied, stale, or bound to a different package, mod identity, or game build.",
                binding);
        }

        if (request.Approval is null)
        {
            return new CodeModAdmissionDecision(
                CodeModAdmissionStatus.RequiresExplicitApproval,
                CodeModAdmissionReasonCodes.ApprovalRequired,
                "Explicit approval for this exact package and game build is required before a full-trust code mod can proceed.",
                binding);
        }

        return new CodeModAdmissionDecision(
            CodeModAdmissionStatus.Approved,
            CodeModAdmissionReasonCodes.Approved,
            "Admission passed for the exact bound artifact; a future loader may continue, but no assembly was loaded by this gate.",
            binding);
    }

    private static bool HasValidRequestShape(CodeModActivationRequest request) =>
        request.Descriptor is not null
        && request.Descriptor.Identity is not null
        && request.IntegrityEvidence is not null
        && request.IntegrityEvidence.ModIdentity is not null
        && request.CompatibilityEvidence is not null
        && request.GameFingerprint is not null
        && request.GameFingerprint.Value is not null
        && request.CompatibilityEvidence.GameFingerprint is not null
        && request.CompatibilityEvidence.GameFingerprint.Value is not null;

    private static bool MatchesApprovedBinding(
        CodeModActivationRequest request,
        CodeModAdmissionBinding binding) =>
        request.Approval!.Decision == CodeModApprovalDecision.Approved
        && request.Approval.Scope == CodeModApprovalScope.ExactPackageAndGameBuild
        && request.Approval.ModIdentity == binding.ModIdentity
        && request.Approval.PackageSha256 == binding.PackageSha256
        && string.Equals(
            request.Approval.GameFingerprint.Value,
            binding.GameFingerprint.Value,
            StringComparison.Ordinal);

    private static CodeModAdmissionDecision Rejected(
        string reasonCode,
        string message,
        CodeModAdmissionBinding? binding) =>
        new(CodeModAdmissionStatus.Rejected, reasonCode, message, binding);
}

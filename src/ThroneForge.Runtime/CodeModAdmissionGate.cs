using ThroneForge.Contracts;

namespace ThroneForge.Runtime;

public enum CodeModAdmissionStatus
{
    Rejected = 0,
    RequiresExplicitApproval,
    Approved
}

public sealed record CodeModAdmissionDecision(
    CodeModAdmissionStatus Status,
    string ReasonCode,
    string Message);

public static class CodeModAdmissionGate
{
    public static CodeModAdmissionDecision Evaluate(CodeModActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Mod.HasValidPackageSha256)
        {
            return Rejected(
                "TF-RUN-009",
                "The code-mod package SHA-256 is not a valid 64-character hexadecimal digest.");
        }

        if (!request.PackageIntegrityVerified)
        {
            return Rejected(
                "TF-RUN-010",
                "The code-mod package integrity has not been verified; activation is blocked.");
        }

        if (request.AdapterCompatibility != AdapterCompatibility.Supported)
        {
            return Rejected(
                "TF-ADP-001",
                "The current adapter compatibility state does not permit full-trust code-mod activation.");
        }

        if (!request.ExplicitApproval)
        {
            return new CodeModAdmissionDecision(
                CodeModAdmissionStatus.RequiresExplicitApproval,
                "TF-RUN-011",
                "Explicit user approval is required before a full-trust code mod can proceed.");
        }

        return new CodeModAdmissionDecision(
            CodeModAdmissionStatus.Approved,
            "TF-RUN-012",
            "Admission passed; a future loader may continue, but no assembly was loaded by this gate.");
    }

    private static CodeModAdmissionDecision Rejected(string reasonCode, string message) =>
        new(CodeModAdmissionStatus.Rejected, reasonCode, message);
}

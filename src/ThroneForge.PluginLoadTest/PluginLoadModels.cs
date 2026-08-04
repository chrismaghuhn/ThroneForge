using ThroneForge.Contracts;

namespace ThroneForge.PluginLoadTest;

public enum PluginLoadStatus
{
    Rejected = 0,
    Loaded,
    Failed
}

public static class PluginLoadReasonCodes
{
    public const string InvalidRequest = "TF-LOAD-000";
    public const string ArtifactUnavailable = "TF-LOAD-001";
    public const string ArtifactTooLarge = "TF-LOAD-002";
    public const string ArtifactUnreadable = "TF-LOAD-003";
    public const string AssemblyLoadFailed = "TF-LOAD-004";
    public const string ContractMissing = "TF-LOAD-005";
    public const string ContractAmbiguous = "TF-LOAD-006";
}

public sealed record PluginLoadRequest(
    string ArtifactPath,
    CodeModDescriptor Descriptor,
    GameFingerprint GameFingerprint,
    CodeModApprovalRecord? Approval,
    AdapterCompatibilityEvidence CompatibilityEvidence);

public sealed record PluginLoadResult(
    PluginLoadStatus Status,
    string ReasonCode,
    string Message,
    CodeModAdmissionBinding? Binding,
    string? AssemblyName,
    IReadOnlyList<string> ImplementedContractTypes);

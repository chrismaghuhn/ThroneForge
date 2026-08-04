using ThroneForge.Contracts;

namespace ThroneForge.PluginLoadTest;

public enum PluginLoadStatus
{
    Rejected = 0,
    Loaded,
    Failed
}

public enum PluginUnloadStatus
{
    NotAttempted = 0,
    UnloadRequested,
    UnloadObserved,
    UnloadNotObservedWithinBound
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
    public const string ManagedDependencyNotAllowed = "TF-LOAD-007";
    public const string NativeDependencyNotAllowed = "TF-LOAD-008";
    public const string ModuleInitializerNotAllowed = "TF-LOAD-009";
    public const string ContractInvalid = "TF-LOAD-010";
    public const string UnloadNotObserved = "TF-LOAD-011";
}

public static class PluginContractIssueCodes
{
    public const string Internal = "internal-type";
    public const string Nested = "nested-type";
    public const string Abstract = "abstract-type";
    public const string OpenGeneric = "open-generic-type";
    public const string Interface = "interface-type";
}

public sealed record PluginLoadRequest(
    string ArtifactPath,
    CodeModDescriptor Descriptor,
    GameFingerprint GameFingerprint,
    CodeModApprovalRecord? Approval,
    AdapterCompatibilityEvidence CompatibilityEvidence);

public sealed record PluginLoadClosureEvidence(
    Sha256Digest PrimaryArtifactSha256,
    IReadOnlyList<string> SharedAssemblyIdentities,
    IReadOnlyList<string> TrustedPlatformAssemblyReferences,
    IReadOnlyList<string> NonPlatformAssemblyReferences,
    bool NativeDependenciesDetected);

public sealed record PluginLoadResult(
    PluginLoadStatus Status,
    string ReasonCode,
    string Message,
    CodeModAdmissionBinding? Binding,
    string? AssemblyName,
    IReadOnlyList<string> ImplementedContractTypes,
    PluginUnloadStatus UnloadStatus,
    bool UnloadRequested,
    PluginLoadClosureEvidence? ClosureEvidence,
    IReadOnlyList<string> ContractIssues);

public sealed class PluginArtifactCapture
{
    private readonly byte[] bytes;

    internal PluginArtifactCapture(string canonicalPath, byte[] bytes, Sha256Digest sha256)
    {
        CanonicalPath = canonicalPath;
        this.bytes = bytes.ToArray();
        Sha256 = sha256;
    }

    public string CanonicalPath { get; }

    public Sha256Digest Sha256 { get; }

    public ReadOnlyMemory<byte> Bytes => bytes;
}

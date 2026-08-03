namespace ThroneForge.Discovery;

public enum BackendClassification
{
    Mono,
    IL2CPP,
    Ambiguous,
    Unknown
}

public enum ExecutableArchitecture
{
    X86,
    X64,
    Arm64,
    Unknown
}

public enum ManagedRuntimeProfile
{
    Mono,
    IL2CPP,
    Conflicting,
    Unknown
}

public enum TargetFrameworkRecommendation
{
    Netstandard21Candidate,
    Netstandard20Candidate,
    Net472FallbackCandidate,
    Net46Candidate,
    Net35FallbackCandidate,
    FrameworkCompatibleButExactTfmUnresolved,
    Conflicting,
    Unknown
}

public enum LoaderIndicatorStatus
{
    Absent,
    Present,
    Ambiguous,
    PotentialConflict
}

public sealed record DiscoveryRequest(
    string GamePath,
    string OutputRoot,
    bool OverwriteExisting = false,
    DateTimeOffset? DiscoveryTimestampUtc = null);

public sealed record EvidenceItem(string Category, string RelativePath, string Description);

public sealed record SelectedFileEvidence(string RelativePath, long Size, string Sha256);

public sealed record RuntimeCompatibilityRequest(
    string GamePath,
    string BaseFingerprint,
    string OutputRoot,
    bool OverwriteExisting = false,
    DateTimeOffset? DiscoveryTimestampUtc = null);

public sealed record FrameworkAssemblyReference(string Name, Version? Version);

public sealed record ManagedAssemblyEvidence(
    string RelativePath,
    bool HasManagedMetadata,
    string? AssemblyName,
    Version? AssemblyVersion,
    string? TargetFramework,
    IReadOnlyList<FrameworkAssemblyReference> SelectedFrameworkReferences,
    string? FailureReason);

public sealed record RuntimeLayoutEvidence(
    string RelativePath,
    bool IsDirectory,
    bool Present,
    string Description);

public sealed record UnityVersionEvidence(
    string Source,
    string RelativePath,
    string Version,
    string Description);

public sealed record LoaderIndicatorEvidence(
    string Name,
    string RelativePath,
    LoaderIndicatorStatus Status,
    string Explanation);

public sealed record BepInExCandidate(
    string Product,
    string Version,
    string OfficialStatus,
    string BackendMatch,
    string ArchitectureMatch,
    string LikelyTargetFramework,
    string Stability,
    string KnownUncertainty,
    string Suitability,
    string SourceTitle,
    string SourceUrl,
    string RetrievedDateUtc);

public sealed record RuntimeCompatibilityResult(
    string BaseFingerprint,
    string InspectionToolVersion,
    string BackendEvidenceReference,
    ExecutableArchitecture ExecutableArchitecture,
    string? SelectedExecutableRelativePath,
    ManagedRuntimeProfile ManagedRuntimeProfile,
    IReadOnlyList<RuntimeLayoutEvidence> RuntimeLayoutEvidence,
    IReadOnlyList<ManagedAssemblyEvidence> ManagedAssemblies,
    TargetFrameworkRecommendation TargetFrameworkRecommendation,
    string RecommendationConfidence,
    string UnityVersion,
    IReadOnlyList<UnityVersionEvidence> UnityVersionEvidence,
    IReadOnlyList<LoaderIndicatorEvidence> LoaderIndicators,
    IReadOnlyList<BepInExCandidate> BepInExCandidates,
    string RecommendedCandidate,
    IReadOnlyList<string> MissingOrConflictingEvidence,
    string ReportPath,
    string ReportMarkdown);

public sealed record DiscoveryResult(
    string Fingerprint,
    string DiscoveryToolVersion,
    string FingerprintAlgorithmVersion,
    BackendClassification Backend,
    ExecutableArchitecture ExecutableArchitecture,
    string UnityVersion,
    IReadOnlyList<EvidenceItem> DetectedEvidence,
    IReadOnlyList<string> MissingOrConflictingEvidence,
    IReadOnlyList<SelectedFileEvidence> SelectedFiles,
    string ReportPath,
    string ReportMarkdown);

public sealed class DiscoveryException : Exception
{
    public DiscoveryException(string message)
        : base(message)
    {
    }

    public DiscoveryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

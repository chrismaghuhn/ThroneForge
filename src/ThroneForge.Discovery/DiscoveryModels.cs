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

public sealed record DiscoveryRequest(
    string GamePath,
    string OutputRoot,
    bool OverwriteExisting = false,
    DateTimeOffset? DiscoveryTimestampUtc = null);

public sealed record EvidenceItem(string Category, string RelativePath, string Description);

public sealed record SelectedFileEvidence(string RelativePath, long Size, string Sha256);

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

namespace ThroneForge.Contracts;

public sealed record GameFingerprint(string Value);

public enum AdapterCompatibility
{
    Unknown = 0,
    Supported,
    SupportedWithWarnings,
    UnknownBuild,
    MissingCriticalBindings,
    PartiallySupported,
    UnsupportedBackend,
    InitializationFailed
}

public sealed record AdapterCapabilities(IReadOnlySet<string> FeatureKeys);

public sealed record GameCatalog(string Fingerprint);

public sealed record WaveDefinition(string ContentId);

public sealed record WaveValidationResult(IReadOnlyList<ValidationIssue> Issues);

public sealed record WaveHandle(string Value);

public sealed record ValidationIssue(string Code, string Message);


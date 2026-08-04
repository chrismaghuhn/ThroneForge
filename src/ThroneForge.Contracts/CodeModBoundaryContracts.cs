namespace ThroneForge.Contracts;

public sealed record ModIdentity
{
    public ModIdentity(string id, string version)
    {
        Id = RequireValue(id, nameof(id));
        Version = RequireValue(version, nameof(version));
    }

    public string Id { get; }

    public string Version { get; }

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The value must not be empty.", parameterName)
            : value;
}

public sealed record CodeModDescriptor
{
    public CodeModDescriptor(ModIdentity identity, string packageSha256)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        PackageSha256 = NormalizeHash(packageSha256);
    }

    public ModIdentity Identity { get; }

    public string PackageSha256 { get; }

    public bool HasValidPackageSha256 =>
        PackageSha256.Length == 64 && PackageSha256.All(IsHexCharacter);

    private static string NormalizeHash(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("The package SHA-256 must not be empty.", nameof(value))
            : value.Trim().ToLowerInvariant();

    private static bool IsHexCharacter(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';
}

public sealed record CodeModActivationRequest
{
    public CodeModActivationRequest(
        CodeModDescriptor mod,
        GameFingerprint gameFingerprint,
        AdapterCompatibility adapterCompatibility,
        bool packageIntegrityVerified,
        bool explicitApproval)
    {
        Mod = mod ?? throw new ArgumentNullException(nameof(mod));
        GameFingerprint = gameFingerprint ?? throw new ArgumentNullException(nameof(gameFingerprint));
        AdapterCompatibility = adapterCompatibility;
        PackageIntegrityVerified = packageIntegrityVerified;
        ExplicitApproval = explicitApproval;
    }

    public CodeModDescriptor Mod { get; }

    public GameFingerprint GameFingerprint { get; }

    public AdapterCompatibility AdapterCompatibility { get; }

    public bool PackageIntegrityVerified { get; }

    public bool ExplicitApproval { get; }
}

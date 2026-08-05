using System.Security.Cryptography;
using System.Text;

namespace ThroneForge.Contracts;

public static class CodeModBoundaryValueRules
{
    public const int MaxModIdLength = 64;
    public const int MaxVersionLength = 64;
    public const int MaxVerificationMethodLength = 64;

    public static string NormalizeModId(string value)
    {
        var canonical = NormalizeTrimmed(value, nameof(value), MaxModIdLength, "mod ID");
        if (canonical.Length < 3 || !IsAsciiAlphaNumeric(canonical[0]))
        {
            throw new ArgumentException("The mod ID must start with a letter or digit and contain at least three characters.", nameof(value));
        }

        for (var index = 0; index < canonical.Length; index++)
        {
            var character = canonical[index];
            if (!IsAsciiAlphaNumeric(character) && character is not ('.' or '_' or '-'))
            {
                throw new ArgumentException("The mod ID contains an unsupported character.", nameof(value));
            }

            if (character == '.' && (index == 0 || index == canonical.Length - 1 || canonical[index - 1] == '.'))
            {
                throw new ArgumentException("The mod ID must not contain empty dot-separated components.", nameof(value));
            }
        }

        return canonical;
    }

    public static string NormalizeVersion(string value)
    {
        var canonical = NormalizeTrimmed(value, nameof(value), MaxVersionLength, "version");
        var buildSeparator = canonical.IndexOf('+');
        var withoutBuild = buildSeparator < 0 ? canonical : canonical[..buildSeparator];
        var build = buildSeparator < 0 ? null : canonical[(buildSeparator + 1)..];
        var preReleaseSeparator = withoutBuild.IndexOf('-');
        var core = preReleaseSeparator < 0 ? withoutBuild : withoutBuild[..preReleaseSeparator];
        var preRelease = preReleaseSeparator < 0 ? null : withoutBuild[(preReleaseSeparator + 1)..];
        var coreParts = core.Split('.');

        if (coreParts.Length != 3 || coreParts.Any(part => !IsNumericPart(part)))
        {
            throw new ArgumentException("The version must use a major.minor.patch form.", nameof(value));
        }

        ValidateIdentifierList(preRelease, allowNumericLeadingZero: false, nameof(value));
        ValidateIdentifierList(build, allowNumericLeadingZero: true, nameof(value));
        return canonical;
    }

    public static string NormalizeGameFingerprint(GameFingerprint value)
    {
#if NETSTANDARD2_1
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentNullException.ThrowIfNull(value);
#endif
        return new Sha256Digest(value.Value).Value;
    }

    public static string NormalizeAdapterId(string value) => NormalizeModId(value);

    public static string NormalizeVerificationMethod(string value) =>
        NormalizeToken(value, MaxVerificationMethodLength, nameof(value), "verification method");

    private static string NormalizeTrimmed(string value, string parameterName, int maxLength, string description)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException($"The {description} must not contain control characters.", parameterName);
        }

        var canonical = value.Trim().ToLowerInvariant();
        if (canonical.Length == 0 || canonical.Length > maxLength)
        {
            throw new ArgumentException($"The {description} must contain between 1 and {maxLength} characters.", parameterName);
        }

        if (canonical.Any(char.IsWhiteSpace) || canonical.Contains('/') || canonical.Contains('\\') || canonical.Contains(':'))
        {
            throw new ArgumentException($"The {description} must not contain whitespace, path separators, or device-path syntax.", parameterName);
        }

        return canonical;
    }

    private static string NormalizeToken(string value, int maxLength, string parameterName, string description)
    {
        var canonical = NormalizeTrimmed(value, parameterName, maxLength, description);
        if (canonical.Any(character => !IsAsciiAlphaNumeric(character) && character is not ('.' or '_' or '-')))
        {
            throw new ArgumentException($"The {description} contains an unsupported character.", parameterName);
        }

        return canonical;
    }

    private static void ValidateIdentifierList(string? value, bool allowNumericLeadingZero, string parameterName)
    {
        if (value is null)
        {
            return;
        }

        var identifiers = value.Split('.');
        if (identifiers.Any(identifier => identifier.Length == 0))
        {
            throw new ArgumentException("Version identifiers must not be empty.", parameterName);
        }

        foreach (var identifier in identifiers)
        {
            if (identifier.Any(character => !IsAsciiAlphaNumeric(character) && character != '-'))
            {
                throw new ArgumentException("Version identifiers contain unsupported characters.", parameterName);
            }

            if (!allowNumericLeadingZero && IsAllDigits(identifier) && identifier.Length > 1 && identifier[0] == '0')
            {
                throw new ArgumentException("Numeric version identifiers must not have leading zeroes.", parameterName);
            }
        }
    }

    private static bool IsNumericPart(string value) =>
        value.Length > 0 && value.All(character => character is >= '0' and <= '9')
        && (value.Length == 1 || value[0] != '0');

    private static bool IsAllDigits(string value) =>
        value.Length > 0 && value.All(character => character is >= '0' and <= '9');

    private static bool IsAsciiAlphaNumeric(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';
}

public readonly record struct Sha256Digest
{
    public Sha256Digest(string value)
    {
#if NETSTANDARD2_1
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#else
        ArgumentNullException.ThrowIfNull(value);
#endif

        var canonical = value.Trim().ToLowerInvariant();
        if (canonical.Length != 64 || canonical.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("The value must be a 64-character hexadecimal SHA-256 digest.", nameof(value));
        }

        Value = canonical;
    }

    public string Value { get; }

    public bool IsValid => Value is { Length: 64 } && Value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public override string ToString() => Value;
}

public sealed record ModIdentity
{
    public ModIdentity(string id, string version)
    {
        Id = CodeModBoundaryValueRules.NormalizeModId(id);
        Version = CodeModBoundaryValueRules.NormalizeVersion(version);
    }

    public string Id { get; }

    public string Version { get; }
}

public sealed record CodeModDescriptor
{
    public CodeModDescriptor(ModIdentity identity, Sha256Digest packageSha256)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        if (!packageSha256.IsValid)
        {
            throw new ArgumentException("The package SHA-256 must be a valid digest.", nameof(packageSha256));
        }

        PackageSha256 = packageSha256;
    }

    public CodeModDescriptor(ModIdentity identity, string packageSha256)
        : this(identity, new Sha256Digest(packageSha256))
    {
    }

    public ModIdentity Identity { get; }

    public Sha256Digest PackageSha256 { get; }

    public bool HasValidPackageSha256 => PackageSha256.IsValid;
}

public enum CodeModIntegrityVerificationStatus
{
    Unverified = 0,
    Verified,
    Failed
}

public sealed record CodeModIntegrityEvidence
{
    public CodeModIntegrityEvidence(
        ModIdentity modIdentity,
        Sha256Digest expectedPackageSha256,
        Sha256Digest observedPackageSha256,
        CodeModIntegrityVerificationStatus status,
        string verificationMethod)
    {
        ModIdentity = modIdentity ?? throw new ArgumentNullException(nameof(modIdentity));
        if (!expectedPackageSha256.IsValid || !observedPackageSha256.IsValid)
        {
            throw new ArgumentException("Integrity evidence must contain valid SHA-256 digests.", nameof(expectedPackageSha256));
        }

        ExpectedPackageSha256 = expectedPackageSha256;
        ObservedPackageSha256 = observedPackageSha256;
        Status = status;
        VerificationMethod = CodeModBoundaryValueRules.NormalizeVerificationMethod(verificationMethod);
    }

    public ModIdentity ModIdentity { get; }

    public Sha256Digest ExpectedPackageSha256 { get; }

    public Sha256Digest ObservedPackageSha256 { get; }

    public CodeModIntegrityVerificationStatus Status { get; }

    public string VerificationMethod { get; }
}

public enum CodeModApprovalDecision
{
    Denied = 0,
    Approved
}

public enum CodeModApprovalScope
{
    ExactPackageAndGameBuild = 0
}

public sealed record CodeModApprovalRecord
{
    public CodeModApprovalRecord(
        ModIdentity modIdentity,
        Sha256Digest packageSha256,
        GameFingerprint gameFingerprint,
        CodeModApprovalDecision decision,
        CodeModApprovalScope scope,
        DateTimeOffset recordedAtUtc)
    {
        ModIdentity = modIdentity ?? throw new ArgumentNullException(nameof(modIdentity));
        if (!packageSha256.IsValid)
        {
            throw new ArgumentException("The package SHA-256 must be a valid digest.", nameof(packageSha256));
        }

        PackageSha256 = packageSha256;
        GameFingerprint = new GameFingerprint(CodeModBoundaryValueRules.NormalizeGameFingerprint(gameFingerprint));
        Decision = decision;
        Scope = scope;
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
    }

    public ModIdentity ModIdentity { get; }

    public Sha256Digest PackageSha256 { get; }

    public GameFingerprint GameFingerprint { get; }

    public CodeModApprovalDecision Decision { get; }

    public CodeModApprovalScope Scope { get; }

    public DateTimeOffset RecordedAtUtc { get; }
}

public sealed record AdapterCompatibilityEvidence
{
    public AdapterCompatibilityEvidence(
        GameFingerprint gameFingerprint,
        string adapterId,
        string adapterVersion,
        AdapterCompatibility compatibility)
    {
        GameFingerprint = new GameFingerprint(CodeModBoundaryValueRules.NormalizeGameFingerprint(gameFingerprint));
        AdapterId = CodeModBoundaryValueRules.NormalizeAdapterId(adapterId);
        AdapterVersion = CodeModBoundaryValueRules.NormalizeVersion(adapterVersion);
        Compatibility = compatibility;
    }

    public GameFingerprint GameFingerprint { get; }

    public string AdapterId { get; }

    public string AdapterVersion { get; }

    public AdapterCompatibility Compatibility { get; }
}

public sealed record CodeModActivationRequest
{
    public CodeModActivationRequest(
        CodeModDescriptor descriptor,
        GameFingerprint gameFingerprint,
        CodeModIntegrityEvidence integrityEvidence,
        CodeModApprovalRecord? approval,
        AdapterCompatibilityEvidence compatibilityEvidence)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        GameFingerprint = new GameFingerprint(CodeModBoundaryValueRules.NormalizeGameFingerprint(gameFingerprint));
        IntegrityEvidence = integrityEvidence ?? throw new ArgumentNullException(nameof(integrityEvidence));
        Approval = approval;
        CompatibilityEvidence = compatibilityEvidence ?? throw new ArgumentNullException(nameof(compatibilityEvidence));
    }

    public CodeModDescriptor Descriptor { get; }

    public GameFingerprint GameFingerprint { get; }

    public CodeModIntegrityEvidence IntegrityEvidence { get; }

    public CodeModApprovalRecord? Approval { get; }

    public AdapterCompatibilityEvidence CompatibilityEvidence { get; }
}

public sealed record CodeModAdmissionBinding
{
    public CodeModAdmissionBinding(
        ModIdentity modIdentity,
        Sha256Digest packageSha256,
        GameFingerprint gameFingerprint,
        string adapterId,
        string adapterVersion)
    {
        ModIdentity = modIdentity ?? throw new ArgumentNullException(nameof(modIdentity));
        if (!packageSha256.IsValid)
        {
            throw new ArgumentException("The package SHA-256 must be a valid digest.", nameof(packageSha256));
        }

        PackageSha256 = packageSha256;
        GameFingerprint = new GameFingerprint(CodeModBoundaryValueRules.NormalizeGameFingerprint(gameFingerprint));
        AdapterId = CodeModBoundaryValueRules.NormalizeAdapterId(adapterId);
        AdapterVersion = CodeModBoundaryValueRules.NormalizeVersion(adapterVersion);
        BindingDigest = ComputeDigest();
    }

    public ModIdentity ModIdentity { get; }

    public Sha256Digest PackageSha256 { get; }

    public GameFingerprint GameFingerprint { get; }

    public string AdapterId { get; }

    public string AdapterVersion { get; }

    public string BindingDigest { get; }

    private string ComputeDigest()
    {
        var canonical = string.Join(
            '\n',
            "throneforge-code-mod-admission-binding-v1",
            ModIdentity.Id,
            ModIdentity.Version,
            PackageSha256.Value,
            GameFingerprint.Value,
            AdapterId,
            AdapterVersion);
        return PortableContractUtilities.ToLowerHex(PortableContractUtilities.ComputeSha256(Encoding.UTF8.GetBytes(canonical)));
    }
}

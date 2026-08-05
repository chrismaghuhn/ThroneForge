using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ThroneForge.Contracts;
using ThroneForge.Discovery;
using ThroneForge.LoaderSmokeTest;
using ThroneForge.Runtime;

namespace ThroneForge.PluginSmokeTest;

public class PluginSmokeException : Exception
{
    public PluginSmokeException(string message)
        : base(message)
    {
    }

    public PluginSmokeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class PluginDeploymentVerificationException : PluginSmokeException
{
    public PluginDeploymentVerificationException(string message)
        : base(message)
    {
    }

    public PluginDeploymentVerificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class PluginSmokePhaseException : PluginSmokeException
{
    public PluginSmokePhaseException(string phase, string failureCategory, string message, Exception? innerException = null)
        : base(message, innerException ?? new InvalidOperationException(message))
    {
        Phase = phase;
        FailureCategory = failureCategory;
    }

    public string Phase { get; }

    public string FailureCategory { get; }
}

public static class PluginSmokeStateFailureCategories
{
    public const string OwnershipStateInvalid = "ownership-state-invalid";
    public const string BaselineStateMissing = "baseline-state-missing";
    public const string BaselineStateMismatch = "baseline-state-mismatch";
    public const string TransactionStateMissing = "transaction-state-missing";
    public const string TransactionStateMismatch = "transaction-state-mismatch";
    public const string AppliedProfileDrift = "applied-profile-drift";
    public const string FingerprintMismatch = "fingerprint-mismatch";
    public const string ExistingPlugin = "existing-plugin";
    public const string ProcessActive = "process-active";
    public const string BootstrapEvidenceInvalid = "bootstrap-evidence-invalid";
}

public sealed class PluginSmokeStateException : PluginSmokeException
{
    public PluginSmokeStateException(string failureCategory, string message)
        : base(message)
    {
        FailureCategory = failureCategory;
    }

    public PluginSmokeStateException(string failureCategory, string message, Exception innerException)
        : base(message, innerException)
    {
        FailureCategory = failureCategory;
    }

    public string FailureCategory { get; }
}

public enum PluginTargetFramework
{
    Inconclusive = 0,
    Netstandard20Candidate,
    Netstandard21Candidate
}

public enum PluginTfmConfidence
{
    None = 0,
    Low,
    Medium,
    High
}

public sealed record ManagedAssemblyCompatibilityEvidence(
    string RelativePath,
    string AssemblyIdentity,
    string? TargetFramework,
    bool ManagedMetadataPresent,
    bool SupportsNetstandard);

public sealed record PluginTfmAssessment(
    PluginTargetFramework Recommendation,
    PluginTfmConfidence Confidence,
    string Basis);

public static class PluginTargetFrameworkSelector
{
    public static PluginTfmAssessment Select(
        IReadOnlyList<ManagedAssemblyCompatibilityEvidence> assemblies,
        string? unityVersion)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var direct = assemblies
            .Select(assembly => assembly.TargetFramework?.Trim().ToLowerInvariant())
            .Where(value => value is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (direct.Length > 1)
        {
            return new(PluginTargetFramework.Inconclusive, PluginTfmConfidence.None, "Conflicting direct target-framework metadata.");
        }

        if (direct.SingleOrDefault() is "netstandard2.0")
        {
            return new(PluginTargetFramework.Netstandard20Candidate, PluginTfmConfidence.High, "Direct target-framework metadata.");
        }

        if (direct.SingleOrDefault() is "netstandard2.1")
        {
            return new(PluginTargetFramework.Netstandard21Candidate, PluginTfmConfidence.High, "Direct target-framework metadata.");
        }

        if (!assemblies.Any(assembly => assembly.SupportsNetstandard))
        {
            return new(PluginTargetFramework.Inconclusive, PluginTfmConfidence.None, "No usable netstandard compatibility evidence.");
        }

        if (!TryParseUnityVersion(unityVersion, out var major, out var minor))
        {
            return new(PluginTargetFramework.Inconclusive, PluginTfmConfidence.None, "Unity version evidence is unavailable.");
        }

        return major > 2021 || major == 2021 && minor >= 2
            ? new(PluginTargetFramework.Netstandard21Candidate, PluginTfmConfidence.Medium, "netstandard compatibility surface plus Unity 2021.2-or-newer evidence.")
            : new(PluginTargetFramework.Netstandard20Candidate, PluginTfmConfidence.Medium, "netstandard compatibility surface plus Unity 2021.1-or-older evidence.");
    }

    private static bool TryParseUnityVersion(string? value, out int major, out int minor)
    {
        var match = value is null ? null : Regex.Match(value, "^(?<major>\\d+)\\.(?<minor>\\d+)", RegexOptions.CultureInvariant);
        if (match is null || !match.Success
            || !int.TryParse(match.Groups["major"].Value, out major)
            || !int.TryParse(match.Groups["minor"].Value, out minor))
        {
            major = 0;
            minor = 0;
            return false;
        }

        return true;
    }
}

public sealed record PluginPackageFile(
    string RelativePath,
    long Size,
    Sha256Digest Sha256,
    string AssemblyIdentity,
    string TargetFramework);

public sealed record PluginPackageManifest(
    string SchemaVersion,
    ModIdentity Identity,
    IReadOnlyList<PluginPackageFile> Files,
    Sha256Digest PackageSha256);

public static class PluginPackageManifestService
{
    public const string SchemaVersion = "throneforge-synthetic-plugin-package-v1";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static PluginPackageManifest Create(ModIdentity identity, IReadOnlyList<PluginPackageFile> files)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new PluginSmokeException("The synthetic plugin package must contain at least one file.");
        }

        var normalized = files
            .Select(file => NormalizeFile(file))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Select(file => file.RelativePath).Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new PluginSmokeException("The synthetic plugin package contains duplicate relative paths.");
        }

        var provisional = new PluginPackageManifest(SchemaVersion, identity, normalized, new Sha256Digest(new string('0', 64)));
        return provisional with { PackageSha256 = ComputeDigest(provisional) };
    }

    public static Sha256Digest ComputeDigest(PluginPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var canonical = new StringBuilder()
            .Append(SchemaVersion).Append('\n')
            .Append(manifest.Identity.Id).Append('\n')
            .Append(manifest.Identity.Version).Append('\n');

        foreach (var file in manifest.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
        {
            canonical.Append(file.RelativePath).Append('\n')
                .Append(file.Size.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n')
                .Append(file.Sha256.Value).Append('\n')
                .Append(file.AssemblyIdentity).Append('\n')
                .Append(file.TargetFramework).Append('\n');
        }

        return new Sha256Digest(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant());
    }

    public static PluginPackageManifest CreateFromDirectory(
        string packageRoot,
        ModIdentity identity,
        IReadOnlyList<string> relativePaths,
        string targetFramework)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        if (!relativePaths.ToHashSet(StringComparer.Ordinal).SetEquals(PluginAdmissionService.ExpectedPackagePaths)
            || relativePaths.Count != PluginAdmissionService.ExpectedPackagePaths.Length)
        {
            throw new PluginSmokeException("The synthetic plugin package must contain exactly the three Task-6 assembly paths.");
        }

        var normalizedRoot = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new PluginSmokeException("The synthetic plugin package directory does not exist.");
        }

        SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(normalizedRoot);
        var actualFiles = Directory.EnumerateFiles(normalizedRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(normalizedRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualFiles.SequenceEqual(PluginAdmissionService.ExpectedPackagePaths.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new PluginSmokeException("The synthetic plugin package contains additional or renamed files.");
        }

        var files = relativePaths
            .Select(path =>
            {
                ValidateRelativePath(path);
                var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, path.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(fullPath))
                {
                    throw new PluginSmokeException("A declared synthetic plugin package file is missing.");
                }

                var metadata = PluginAssemblyMetadataInspector.Inspect(fullPath, path);
                PluginPackageValidationRules.Validate(path, metadata, targetFramework);

                return new PluginPackageFile(path, metadata.Size, metadata.Sha256, metadata.AssemblyIdentity, targetFramework);
            })
            .ToArray();
        return Create(identity, files);
    }

    private static PluginPackageFile NormalizeFile(PluginPackageFile file)
    {
        ValidateRelativePath(file.RelativePath);
        if (file.Size < 0 || !file.Sha256.IsValid)
        {
            throw new PluginSmokeException("The synthetic plugin package contains invalid file metadata.");
        }

        if (string.IsNullOrWhiteSpace(file.AssemblyIdentity) || file.AssemblyIdentity.Contains('\n', StringComparison.Ordinal))
        {
            throw new PluginSmokeException("The synthetic plugin package contains invalid assembly identity metadata.");
        }

        return file with
        {
            RelativePath = file.RelativePath.Replace('/', '/'),
            AssemblyIdentity = file.AssemblyIdentity.Trim(),
            TargetFramework = file.TargetFramework.Trim().ToLowerInvariant()
        };
    }

    public static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\')
            || relativePath.Contains(':')
            || relativePath.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new PluginSmokeException("The synthetic plugin package contains an unsafe relative path.");
        }
    }

    public static void Save(string path, PluginPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var dto = new PersistedPluginPackageManifest(
            manifest.SchemaVersion,
            manifest.Identity.Id,
            manifest.Identity.Version,
            manifest.PackageSha256.Value,
            manifest.Files.Select(file => new PersistedPluginPackageFile(
                file.RelativePath,
                file.Size,
                file.Sha256.Value,
                file.AssemblyIdentity,
                file.TargetFramework)).ToArray());
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath) ?? throw new PluginSmokeException("The package manifest has no safe parent directory.");
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(dto, JsonOptions), new UTF8Encoding(false));
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static PluginPackageManifest Load(string path)
    {
        PersistedPluginPackageManifest dto;
        try
        {
            dto = JsonSerializer.Deserialize<PersistedPluginPackageManifest>(File.ReadAllText(path))
                ?? throw new PluginSmokeException("The package manifest is empty.");
        }
        catch (PluginSmokeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new PluginSmokeException("The package manifest is missing or malformed.");
        }

        if (!string.Equals(dto.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            throw new PluginSmokeException("The package manifest uses an unsupported schema version.");
        }

        var identity = new ModIdentity(dto.ModId, dto.ModVersion);
        var files = dto.Files.Select(file => new PluginPackageFile(
            file.RelativePath,
            file.Size,
            new Sha256Digest(file.Sha256),
            file.AssemblyIdentity,
            file.TargetFramework)).ToArray();
        var manifest = Create(identity, files);
        if (!manifest.PackageSha256.Value.Equals(dto.PackageSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginSmokeException("The package manifest digest does not match its canonical file metadata.");
        }

        return manifest;
    }

    private sealed record PersistedPluginPackageManifest(
        string SchemaVersion,
        string ModId,
        string ModVersion,
        string PackageSha256,
        IReadOnlyList<PersistedPluginPackageFile> Files);

    private sealed record PersistedPluginPackageFile(
        string RelativePath,
        long Size,
        string Sha256,
        string AssemblyIdentity,
        string TargetFramework);
}

public sealed record PluginAdmissionInputs(
    GameFingerprint GameFingerprint,
    string AdapterId,
    string AdapterVersion,
    DateTimeOffset ApprovalRecordedAtUtc);

public static class PluginAdmissionService
{
    public static CodeModAdmissionDecision EvaluateApprovedPackage(
        PluginPackageManifest package,
        PluginAdmissionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(inputs);
        var descriptor = new CodeModDescriptor(package.Identity, package.PackageSha256);
        var integrity = new CodeModIntegrityEvidence(
            package.Identity,
            package.PackageSha256,
            package.PackageSha256,
            CodeModIntegrityVerificationStatus.Verified,
            "synthetic-package-manifest-v1");
        var compatibility = new AdapterCompatibilityEvidence(
            inputs.GameFingerprint,
            inputs.AdapterId,
            inputs.AdapterVersion,
            AdapterCompatibility.Supported);
        var approval = new CodeModApprovalRecord(
            package.Identity,
            package.PackageSha256,
            inputs.GameFingerprint,
            CodeModApprovalDecision.Approved,
            CodeModApprovalScope.ExactPackageAndGameBuild,
            inputs.ApprovalRecordedAtUtc);
        return CodeModAdmissionGate.Evaluate(new CodeModActivationRequest(
            descriptor,
            inputs.GameFingerprint,
            integrity,
            approval,
            compatibility));
    }

    public static CapturedPluginPackage CaptureAndValidate(
        string packageRoot,
        PluginPackageManifest expectedManifest,
        string targetFramework)
    {
        ArgumentNullException.ThrowIfNull(expectedManifest);
        var expectedPaths = new HashSet<string>(ExpectedPackagePaths, StringComparer.Ordinal);
        if (expectedManifest.Files.Count != expectedPaths.Count
            || expectedManifest.Files.Select(file => file.RelativePath).ToHashSet(StringComparer.Ordinal).SetEquals(expectedPaths) == false)
        {
            throw new PluginSmokeException("The persisted package manifest does not contain the exact Task-6 three-file package shape.");
        }

        var root = Path.GetFullPath(packageRoot);
        if (!Directory.Exists(root))
        {
            throw new PluginSmokeException("The synthetic plugin package directory does not exist.");
        }

        SmokeTestPathValidator.EnsureExistingTreeHasNoReparsePoints(root);
        var actualRelativeFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualRelativeFiles.SequenceEqual(ExpectedPackagePaths.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new PluginSmokeException("The current package contains missing, additional, renamed, or nested files outside the exact Task-6 three-file shape.");
        }

        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var metadata = new Dictionary<string, PluginAssemblyMetadata>(StringComparer.Ordinal);
        var files = new List<PluginPackageFile>();
        foreach (var relativePath in ExpectedPackagePaths)
        {
            var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                throw new PluginSmokeException("The current package is missing one of the exact required assembly files.");
            }

            var captured = CaptureFile(fullPath, DefaultMaximumPackageFileBytes);
            var inspected = PluginAssemblyMetadataInspector.InspectBytes(captured, relativePath);
            PluginPackageValidationRules.Validate(relativePath, inspected, targetFramework);
            bytes.Add(relativePath, captured);
            metadata.Add(relativePath, inspected);
            files.Add(new PluginPackageFile(relativePath, inspected.Size, inspected.Sha256, inspected.AssemblyIdentity, NormalizeTargetFramework(inspected.TargetFramework!)));
        }

        var actual = PluginPackageManifestService.Create(expectedManifest.Identity, files);
        if (!PackageMetadataEquals(expectedManifest, actual))
        {
            throw new PluginSmokeException("The current package bytes do not match the saved expected package manifest.");
        }

        return new CapturedPluginPackage(actual, bytes, metadata);
    }

    public const long DefaultMaximumPackageFileBytes = 64 * 1024 * 1024;
    public static readonly string[] ExpectedPackagePaths =
    [
        "ThroneForge.M1.SyntheticSmoke.dll",
        "ThroneForge.API.dll",
        "ThroneForge.Contracts.dll"
    ];

    private static byte[] CaptureFile(string path, long maximumBytes)
    {
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            if (input.Length > maximumBytes)
            {
                throw new PluginSmokeException("A package file exceeds the bounded read limit.");
            }

            using var output = new MemoryStream(checked((int)input.Length));
            input.CopyTo(output);
            return output.ToArray();
        }
        catch (PluginSmokeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeException("A package file could not be captured safely.", exception);
        }
    }

    private static bool PackageMetadataEquals(PluginPackageManifest expected, PluginPackageManifest actual)
        => expected.Identity == actual.Identity
            && expected.PackageSha256 == actual.PackageSha256
            && expected.Files.Count == actual.Files.Count
            && expected.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Zip(actual.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
                .All(pair => pair.First == pair.Second);

    private static string NormalizeTargetFramework(string value)
        => value.Contains(".NETStandard,Version=v2.0", StringComparison.OrdinalIgnoreCase) ? "netstandard2.0"
            : value.Contains(".NETStandard,Version=v2.1", StringComparison.OrdinalIgnoreCase) ? "netstandard2.1"
            : value.Trim().ToLowerInvariant();
}

public sealed record CapturedPluginPackage(
    PluginPackageManifest Manifest,
    IReadOnlyDictionary<string, byte[]> Bytes,
    IReadOnlyDictionary<string, PluginAssemblyMetadata> Metadata);

public static class PluginPackageValidationRules
{
    private static readonly HashSet<string> CommonReferences = new(StringComparer.Ordinal)
    {
        "System.Runtime", "System.Threading", "System.Threading.Tasks", "System.Collections", "System.Private.CoreLib", "netstandard"
        , "System.Linq", "System.ObjectModel", "System.ComponentModel.Primitives", "System.Runtime.InteropServices"
    };

    public static void Validate(string relativePath, PluginAssemblyMetadata metadata, string targetFramework)
    {
        if (!metadata.HasManagedMetadata || !metadata.ClrHeaderPresent || !metadata.IlOnly
            || metadata.NativeEntryPointPresent || metadata.ManagedNativeHeaderPresent
            || metadata.PInvokeEntryCount != 0 || metadata.ModuleInitializerPresent)
        {
            throw new PluginSmokeException("A package assembly is not a pure managed IL image or contains executable native behavior.");
        }

        var normalizedTargetFramework = targetFramework.Trim().ToLowerInvariant();
        var actualTargetFramework = metadata.TargetFramework is null ? null : NormalizeTargetFramework(metadata.TargetFramework);
        if (!string.Equals(actualTargetFramework, normalizedTargetFramework, StringComparison.Ordinal))
        {
            throw new PluginSmokeException("The declared package target framework does not match assembly metadata.");
        }

        var expectedAssemblyName = Path.GetFileNameWithoutExtension(relativePath);
        var actualAssemblyName = metadata.AssemblyIdentity.Split(',', 2)[0];
        if (!actualAssemblyName.Equals(expectedAssemblyName, StringComparison.Ordinal))
        {
            throw new PluginSmokeException("A package filename does not match its managed assembly identity.");
        }

        var allowed = new HashSet<string>(CommonReferences, StringComparer.Ordinal);
        if (relativePath.Equals("ThroneForge.M1.SyntheticSmoke.dll", StringComparison.Ordinal))
        {
            allowed.UnionWith(["BepInEx", "UnityEngine", "UnityEngine.CoreModule", "ThroneForge.API", "ThroneForge.Contracts"]);
            if (metadata.BepInPluginAttributeCount != 1
                || !metadata.BepInPluginGuid!.Equals("dev.throneforge.m1.synthetic-smoke", StringComparison.Ordinal)
                || !metadata.BepInPluginName!.Equals("ThroneForge M1 Synthetic Smoke", StringComparison.Ordinal)
                || !metadata.BepInPluginVersion!.Equals("0.0.1", StringComparison.Ordinal)
                || metadata.PublicPluginImplementationCount != 1
                || metadata.ThroneForgeModImplementationCount != 1
                || !metadata.HasExpectedBaseUnityPluginReference
                || !metadata.HasThroneForgeModImplementation)
            {
                throw new PluginSmokeException("The synthetic plugin does not contain exactly the expected BepInEx and ThroneForge contract metadata.");
            }
        }
        else if (relativePath.Equals("ThroneForge.API.dll", StringComparison.Ordinal))
        {
            allowed.Add("ThroneForge.Contracts");
        }

        if (metadata.AssemblyReferences.Any(reference => !allowed.Contains(reference)))
        {
            throw new PluginSmokeException("The package contains an unexpected managed assembly reference.");
        }
    }

    private static string NormalizeTargetFramework(string value)
        => value.Contains(".NETStandard,Version=v2.0", StringComparison.OrdinalIgnoreCase) ? "netstandard2.0"
            : value.Contains(".NETStandard,Version=v2.1", StringComparison.OrdinalIgnoreCase) ? "netstandard2.1"
            : value.Trim().ToLowerInvariant();
}

public sealed record PluginDeploymentContext(
    SmokeTestRoots Roots,
    Task6ExperimentState Ownership,
    DisposableProfileBaseline Baseline,
    LoaderTransactionState LoaderTransaction,
    CopyManifest PreDeploymentManifest,
    CodeModAdmissionBinding AdmissionBinding);

public sealed record PluginDeploymentReceipt(
    string RelativeRoot,
    IReadOnlyList<string> DeployedRelativePaths,
    IReadOnlyList<string> DeployedSha256,
    string PackageSha256,
    string AdmissionBindingDigest);

public static class PluginDeploymentService
{
    public static PluginDeploymentContext DeriveContext(
        string originalGameRoot,
        string cleanGameRoot,
        string experimentRoot,
        string repositoryRoot,
        string expectedFingerprint,
        CodeModAdmissionBinding admissionBinding)
    {
        ArgumentNullException.ThrowIfNull(admissionBinding);
        SmokeTestRoots roots;
        try
        {
            roots = SmokeTestPathValidator.ValidateRoots(repositoryRoot, originalGameRoot, experimentRoot);
        }
        catch (SmokeTestException exception)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.OwnershipStateInvalid,
                "The Task-6 deployment ownership roots are not valid.",
                exception);
        }
        if (!Path.GetFullPath(cleanGameRoot).Equals(roots.CleanGameRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.OwnershipStateInvalid,
                "The requested disposable profile is not the owned clean-game path.");
        }

        Task6ExperimentState ownership;
        try
        {
            ownership = Task6ExperimentStateService.LoadAndValidate(experimentRoot, expectedFingerprint);
        }
        catch (PluginSmokeStateException)
        {
            throw;
        }
        catch (PluginSmokeException exception)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.OwnershipStateInvalid,
                "The Task-6 ownership record is invalid for deployment.",
                exception);
        }
        if (ownership.Status is not (Task6ExperimentStatus.LoaderApplied or Task6ExperimentStatus.LaunchObserved))
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.OwnershipStateInvalid,
                "The Task-6 ownership record is not in a loader-ready state for plugin deployment.");
        }

        CopyManifest originalManifest;
        try
        {
            originalManifest = InstallationCopyService.CaptureManifest(roots.OriginalGameRoot);
        }
        catch (SmokeTestException exception)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.OwnershipStateInvalid,
                "The original installation manifest could not be validated for deployment.",
                exception);
        }

        var baselinePath = LoaderSmokeTestStatePaths.GetBaselinePath(roots);
        if (!File.Exists(baselinePath))
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.BaselineStateMissing,
                "The canonical disposable-profile baseline state is missing.");
        }

        DisposableProfileBaseline baseline;
        try
        {
            baseline = DisposableProfileBaselineService.LoadAndValidateSavedBaseline(
                baselinePath,
                expectedFingerprint,
                originalManifest);
        }
        catch (SmokeTestException exception)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.BaselineStateMismatch,
                "The canonical disposable-profile baseline state does not match the original installation.",
                exception);
        }

        CopyManifest current;
        try
        {
            current = InstallationCopyService.CaptureManifest(roots.CleanGameRoot);
        }
        catch (SmokeTestException exception)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.AppliedProfileDrift,
                "The disposable profile manifest could not be validated before deployment.",
                exception);
        }

        var transactionPath = LoaderSmokeTestStatePaths.GetTransactionStatePath(roots);
        if (!File.Exists(transactionPath))
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.TransactionStateMissing,
                "The canonical loader transaction state is missing.");
        }

        LoaderTransactionState loaderState;
        try
        {
            loaderState = LoaderTransactionStateService.LoadAndValidate(
                transactionPath,
                roots,
                expectedFingerprint,
                baseline.DisposableManifest,
                [LoaderTransactionStatus.LaunchObserved]);
        }
        catch (SmokeTestException exception)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.TransactionStateMismatch,
                "The loader transaction state does not match the saved disposable baseline.",
                exception);
        }

        try
        {
            LoaderTransactionStateService.VerifyAppliedProfile(roots, loaderState);
        }
        catch (SmokeTestException exception)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.AppliedProfileDrift,
                "The disposable profile does not match the persisted applied loader state.",
                exception);
        }

        string currentFingerprint;
        try
        {
            currentFingerprint = InstallationFingerprintService.Capture(roots.CleanGameRoot).Fingerprint;
        }
        catch (Exception exception) when (exception is DiscoveryException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.FingerprintMismatch,
                "The disposable profile fingerprint could not be verified.",
                exception);
        }
        if (!currentFingerprint.Equals(expectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.FingerprintMismatch,
                "The disposable profile fingerprint does not match the expected clean-profile evidence.");
        }

        var pluginRoot = Path.Combine(roots.CleanGameRoot, "BepInEx", "plugins");
        if (Directory.Exists(pluginRoot) && Directory.EnumerateFileSystemEntries(pluginRoot).Any())
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.ExistingPlugin,
                "An existing custom plugin is present in the disposable profile.");
        }

        if (FindRunningProcessUnder(roots.CleanGameRoot))
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.ProcessActive,
                "The disposable game process is still active; deployment is refused.");
        }

        if (!loaderState.LaunchEvidence?.MeetsBootstrapCriteria ?? true)
        {
            throw new PluginSmokeStateException(
                PluginSmokeStateFailureCategories.BootstrapEvidenceInvalid,
                "The persisted loader transaction does not prove a clean BepInEx bootstrap.");
        }

        return new PluginDeploymentContext(roots, ownership, baseline, loaderState, current, admissionBinding);
    }

    public static PluginDeploymentReceipt DeployCaptured(
        CapturedPluginPackage package,
        PluginDeploymentContext context,
        int? failAfterFiles = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(context);
        var cleanGameRoot = context.Roots.CleanGameRoot;
        var directory = PluginDeploymentPath.GetPluginDirectory(cleanGameRoot, package.Manifest.Identity.Id);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new PluginSmokeException("The synthetic plugin deployment directory is not empty.");
        }

        var deployedPaths = new List<string>();
        var deployedHashes = new List<string>();
        var destinations = package.Manifest.Files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file =>
            {
                var destinationRelative = $"BepInEx/plugins/{package.Manifest.Identity.Id}/{file.RelativePath}";
                PluginPackageManifestService.ValidateRelativePath(file.RelativePath);
                var destination = Path.GetFullPath(Path.Combine(cleanGameRoot, destinationRelative.Replace('/', Path.DirectorySeparatorChar)));
                SmokeTestPathValidator.EnsureWithin(cleanGameRoot, destination);
                SmokeTestPathValidator.EnsureNoReparsePointsOnPath(destination);
                if (File.Exists(destination) || Directory.Exists(destination))
                {
                    throw new PluginSmokeException("The synthetic plugin deployment destination is unsafe or already occupied.");
                }

                if (!package.Bytes.TryGetValue(file.RelativePath, out var capturedBytes))
                {
                    throw new PluginSmokeException("The exact captured package bytes are incomplete.");
                }

                return (file, destinationRelative, destination, capturedBytes);
            })
            .ToArray();

        var createdFiles = new List<string>();
        try
        {
            var writtenCount = 0;
            foreach (var item in destinations)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.destination)!);
                File.WriteAllBytes(item.destination, item.capturedBytes);
                createdFiles.Add(item.destination);
                writtenCount++;
                if (failAfterFiles is not null && writtenCount >= failAfterFiles.Value)
                {
                    throw new PluginSmokeException("Synthetic deployment failure requested.");
                }
                var observed = new Sha256Digest(Convert.ToHexString(SHA256.HashData(item.capturedBytes)).ToLowerInvariant());
                if (!observed.Equals(item.file.Sha256))
                {
                    throw new PluginSmokeException("A deployed synthetic plugin file did not match the captured package hash.");
                }

                deployedPaths.Add(item.destinationRelative);
                deployedHashes.Add(observed.Value);
            }

            var after = InstallationCopyService.CaptureManifest(cleanGameRoot);
            var expected = AddDeploymentToManifest(context.PreDeploymentManifest, destinations);
            if (!InstallationCopyService.CompareManifests(expected, after).Matches)
            {
                throw new PluginDeploymentVerificationException("The complete disposable manifest did not match the transactional deployment result.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PluginSmokeException)
        {
            foreach (var file in createdFiles.AsEnumerable().Reverse())
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            var parent = directory;
            while (Directory.Exists(parent)
                && !Directory.EnumerateFileSystemEntries(parent).Any()
                && !parent.Equals(Path.Combine(cleanGameRoot, "BepInEx", "plugins"), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                Directory.Delete(parent);
                parent = Path.GetDirectoryName(parent)!;
            }

            if (!InstallationCopyService.CompareManifests(context.PreDeploymentManifest, InstallationCopyService.CaptureManifest(cleanGameRoot)).Matches)
            {
                throw new PluginSmokeException("Synthetic plugin deployment failed and could not restore the complete pre-deployment manifest.", exception);
            }

            throw exception is PluginSmokeException smoke
                ? smoke
                : new PluginSmokeException("Synthetic plugin deployment failed and was rolled back.", exception);
        }

        return new PluginDeploymentReceipt(
            $"BepInEx/plugins/{package.Manifest.Identity.Id}",
            deployedPaths,
            deployedHashes,
            package.Manifest.PackageSha256.Value,
            context.AdmissionBinding.BindingDigest);
    }

    public static void Remove(string cleanGameRoot, string pluginGuid)
    {
        SmokeTestPathValidator.EnsureNoReparsePointsOnPath(cleanGameRoot);
        var directory = PluginDeploymentPath.GetPluginDirectory(cleanGameRoot, pluginGuid);
        if (!Directory.Exists(directory))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(directory).Any(entry =>
                (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0))
        {
            throw new PluginSmokeException("The synthetic plugin deployment contains a reparse point and cannot be removed automatically.");
        }

        Directory.Delete(directory, recursive: true);
    }

    private static CopyManifest AddDeploymentToManifest(
        CopyManifest baseline,
        IReadOnlyList<(PluginPackageFile file, string destinationRelative, string destination, byte[] capturedBytes)> destinations)
    {
        var files = baseline.Files.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
        var directories = (baseline.Directories ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var item in destinations)
        {
            var parts = item.destinationRelative.Split('/');
            for (var index = 1; index < parts.Length; index++)
            {
                directories.Add(string.Join('/', parts[..index]));
            }

            files[item.destinationRelative] = new FileManifestEntry(item.destinationRelative, item.capturedBytes.LongLength, item.file.Sha256.Value);
        }

        return new CopyManifest(files.Values.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray(), directories.Order(StringComparer.Ordinal).ToArray());
    }

    public static bool IsProfileProcessActive(string cleanGameRoot)
        => FindRunningProcessUnder(cleanGameRoot);

    private static bool FindRunningProcessUnder(string cleanGameRoot)
    {
        var root = Path.GetFullPath(cleanGameRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var full = Path.GetFullPath(path);
                    if (full.Equals(root, comparison) || full.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // An inaccessible unrelated process is not evidence that the disposable game is running.
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }
}

public sealed record PluginAssemblyMetadata(
    string RelativePath,
    long Size,
    Sha256Digest Sha256,
    string AssemblyIdentity,
    string? TargetFramework,
    bool HasManagedMetadata,
    bool ClrHeaderPresent,
    bool IlOnly,
    bool NativeEntryPointPresent,
    bool ManagedNativeHeaderPresent,
    int PInvokeEntryCount,
    bool ModuleInitializerPresent,
    IReadOnlyList<string> AssemblyReferences,
    int BepInPluginAttributeCount = 0,
    string? BepInPluginGuid = null,
    string? BepInPluginName = null,
    string? BepInPluginVersion = null,
    int PublicPluginImplementationCount = 0,
    bool HasExpectedBaseUnityPluginReference = false,
    bool HasThroneForgeModImplementation = false,
    int ThroneForgeModImplementationCount = 0);

public static class PluginAssemblyMetadataInspector
{
    public const long DefaultMaximumBytes = 64 * 1024 * 1024;

    public static PluginAssemblyMetadata Inspect(
        string filePath,
        string relativePath,
        long maximumBytes = DefaultMaximumBytes)
    {
        PluginPackageManifestService.ValidateRelativePath(relativePath);
        if (maximumBytes < 1)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1, nameof(maximumBytes));
        }

        byte[] bytes;
        try
        {
            using var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            if (input.Length > maximumBytes)
            {
                throw new PluginSmokeException("A plugin inspection input exceeds the bounded read limit.");
            }

            using var capture = new MemoryStream(capacity: checked((int)input.Length));
            input.CopyTo(capture);
            bytes = capture.ToArray();
        }
        catch (PluginSmokeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new PluginSmokeException("A plugin inspection input could not be read safely.");
        }

        return InspectBytes(bytes, relativePath, maximumBytes);
    }

    public static PluginAssemblyMetadata InspectBytes(
        byte[] bytes,
        string relativePath,
        long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        PluginPackageManifestService.ValidateRelativePath(relativePath);
        if (maximumBytes < 1)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1, nameof(maximumBytes));
        }

        if (bytes.LongLength > maximumBytes)
        {
            throw new PluginSmokeException("A plugin inspection input exceeds the bounded read limit.");
        }

        var digest = new Sha256Digest(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        try
        {
            using var peReader = new PEReader(new MemoryStream(bytes, writable: false));
            if (!peReader.HasMetadata)
            {
                return new(relativePath, bytes.LongLength, digest, string.Empty, null, false, false, false, false, false, 0, false, []);
            }

            var metadata = peReader.GetMetadataReader();
            var corHeader = peReader.PEHeaders.CorHeader;
            var assembly = metadata.GetAssemblyDefinition();
            var assemblyName = metadata.GetString(assembly.Name);
            var assemblyIdentity = $"{assemblyName}, Version={assembly.Version}";
            var references = metadata.AssemblyReferences
                .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                .Order(StringComparer.Ordinal)
                .ToArray();
            var pinvokeCount = metadata.MethodDefinitions.Count(handle =>
                metadata.GetMethodDefinition(handle).Attributes.HasFlag(MethodAttributes.PinvokeImpl));
            var moduleInitializer = HasModuleInitializer(metadata);
            var flags = corHeader?.Flags ?? 0;
            var pluginAttributes = ReadBepInPluginAttributes(metadata, assembly);
            var pluginTypes = ReadPluginTypeEvidence(metadata);
            var modImplementationCount = CountThroneForgeModImplementations(metadata);

            return new(
                relativePath,
                bytes.LongLength,
                digest,
                assemblyIdentity,
                ReadTargetFramework(metadata, assembly),
                true,
                corHeader is not null,
                corHeader is not null && flags.HasFlag(CorFlags.ILOnly),
                corHeader is not null && flags.HasFlag(CorFlags.NativeEntryPoint),
                corHeader?.ManagedNativeHeaderDirectory.Size > 0,
                pinvokeCount,
                moduleInitializer,
                references,
                pluginAttributes.Count,
                pluginAttributes.Count == 1 ? pluginAttributes[0].Guid : null,
                pluginAttributes.Count == 1 ? pluginAttributes[0].Name : null,
                pluginAttributes.Count == 1 ? pluginAttributes[0].Version : null,
                pluginTypes.Count,
                pluginTypes.Any(item => item.HasBaseUnityPlugin),
                pluginTypes.Any(item => item.ImplementsThroneForgeMod),
                modImplementationCount);
        }
        catch (BadImageFormatException)
        {
            throw new PluginSmokeException("A plugin inspection input is not a valid managed PE image.");
        }
        catch (ArgumentException)
        {
            throw new PluginSmokeException("A plugin inspection input contains unsupported metadata.");
        }
    }

    private static List<(string Guid, string Name, string Version)> ReadBepInPluginAttributes(
        MetadataReader metadata,
        AssemblyDefinition assembly)
    {
        var attributes = new List<(string Guid, string Name, string Version)>();
        var handles = assembly.GetCustomAttributes()
            .Concat(metadata.TypeDefinitions.SelectMany(handle => metadata.GetTypeDefinition(handle).GetCustomAttributes()));
        foreach (var handle in handles)
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (!TryGetAttributeTypeName(metadata, attribute.Constructor, out var name)
                || !name.Equals("BepInPlugin", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var reader = metadata.GetBlobReader(attribute.Value);
                if (reader.ReadUInt16() != 1)
                {
                    continue;
                }

                var guid = reader.ReadSerializedString();
                var displayName = reader.ReadSerializedString();
                var version = reader.ReadSerializedString();
                if (guid is not null && displayName is not null && version is not null)
                {
                    attributes.Add((guid, displayName, version));
                }
            }
            catch (BadImageFormatException)
            {
                // Malformed attribute data is represented as absent evidence and rejected by package rules.
            }
        }

        return attributes;
    }

    private static List<(bool HasBaseUnityPlugin, bool ImplementsThroneForgeMod)> ReadPluginTypeEvidence(MetadataReader metadata)
    {
        var evidence = new List<(bool, bool)>();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (!type.Attributes.HasFlag(TypeAttributes.Public)
                || type.Attributes.HasFlag(TypeAttributes.NestedPublic)
                || type.Attributes.HasFlag(TypeAttributes.Abstract)
                || type.Attributes.HasFlag(TypeAttributes.Interface)
                || type.Attributes.HasFlag(TypeAttributes.Sealed) == false
                || type.GetGenericParameters().Count != 0)
            {
                continue;
            }

            var baseName = GetTypeName(metadata, type.BaseType);
            var implementsMod = type.GetInterfaceImplementations()
                .Select(implementation => GetTypeName(metadata, metadata.GetInterfaceImplementation(implementation).Interface))
                .Any(name => name.Equals("IThroneForgeMod", StringComparison.Ordinal));
            if (baseName.Equals("BaseUnityPlugin", StringComparison.Ordinal) || implementsMod)
            {
                evidence.Add((baseName.Equals("BaseUnityPlugin", StringComparison.Ordinal), implementsMod));
            }
        }

        return evidence;
    }

    private static int CountThroneForgeModImplementations(MetadataReader metadata)
    {
        var count = 0;
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (type.GetInterfaceImplementations()
                .Select(implementation => GetTypeName(metadata, metadata.GetInterfaceImplementation(implementation).Interface))
                .Any(name => name.Equals("IThroneForgeMod", StringComparison.Ordinal)))
            {
                count++;
            }
        }

        return count;
    }

    private static string GetTypeName(MetadataReader metadata, EntityHandle handle)
        => handle.Kind switch
        {
            HandleKind.TypeReference => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)handle).Name),
            HandleKind.TypeDefinition => metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
            _ => string.Empty
        };

    private static bool HasModuleInitializer(MetadataReader metadata)
    {
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Name) != "<Module>")
            {
                continue;
            }

            return type.GetMethods().Any(method => metadata.GetString(metadata.GetMethodDefinition(method).Name) == ".cctor");
        }

        return false;
    }

    private static string? ReadTargetFramework(MetadataReader metadata, AssemblyDefinition assembly)
    {
        foreach (var handle in assembly.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (!TryGetAttributeTypeName(metadata, attribute.Constructor, out var name)
                || !name.Equals("TargetFrameworkAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var reader = metadata.GetBlobReader(attribute.Value);
                if (reader.ReadUInt16() == 1)
                {
                    return reader.ReadSerializedString();
                }
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }

        return null;
    }

    private static bool TryGetAttributeTypeName(MetadataReader metadata, EntityHandle constructor, out string name)
    {
        name = string.Empty;
        EntityHandle parent = constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default
        };

        if (parent.IsNil)
        {
            return false;
        }

        name = parent.Kind switch
        {
            HandleKind.TypeReference => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)parent).Name),
            HandleKind.TypeDefinition => metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)parent).Name),
            _ => string.Empty
        };
        return name.Length > 0;
    }
}

public sealed record SyntheticPluginMarker(
    string Nonce,
    string PluginGuid,
    string PluginVersion,
    string ApiIdentity,
    string ContractsIdentity);

public sealed record PluginSmokeMarkerParseResult(
    bool IsValid,
    string FailureCategory,
    int MarkerCount,
    bool LifecycleMarkerDetected,
    string? PluginGuid,
    SyntheticPluginMarker? Marker);

public static class PluginSmokeMarkerParser
{
    private const string ReadyMarker = "THRONEFORGE_SYNTHETIC_PLUGIN_READY";
    private const string LifecycleMarker = "THRONEFORGE_SYNTHETIC_PLUGIN_LIFECYCLE_INVOKED";
    private const string ExpectedGuid = "dev.throneforge.m1.synthetic-smoke";
    private const string ExpectedVersion = "0.0.1";

    public static PluginSmokeMarkerParseResult Parse(
        string text,
        string expectedNonce,
        string expectedGuid = ExpectedGuid,
        string expectedVersion = ExpectedVersion,
        string? expectedApiIdentity = null,
        string? expectedContractsIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedNonce);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var markerLines = lines
            .Where(line => line.Contains(ReadyMarker, StringComparison.Ordinal))
            .ToArray();
        var markerCount = markerLines.Length;
        var lifecycle = lines.Any(line => line.Contains(LifecycleMarker, StringComparison.Ordinal));
        if (markerCount != 1)
        {
            return Invalid("marker-count", markerCount, lifecycle, null);
        }

        var markerPayload = markerLines[0]
            .Split(ReadyMarker, 2, StringSplitOptions.None)[1]
            .Trim()
            .TrimStart('|');
        var valueLines = string.IsNullOrWhiteSpace(markerPayload)
            ? lines
            : markerPayload.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in valueLines.Where(line => line.Contains('=', StringComparison.Ordinal)))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !values.TryAdd(parts[0], parts[1]))
            {
                return Invalid("duplicate-key", markerCount, lifecycle, values.GetValueOrDefault("pluginGuid"));
            }
        }

        if (!values.TryGetValue("nonce", out var nonce) || !nonce.Equals(expectedNonce, StringComparison.Ordinal))
        {
            return Invalid("nonce-mismatch", markerCount, lifecycle, values.GetValueOrDefault("pluginGuid"));
        }

        if (!values.TryGetValue("pluginGuid", out var guid) || !guid.Equals(expectedGuid, StringComparison.Ordinal)
            || !values.TryGetValue("pluginVersion", out var version) || !version.Equals(expectedVersion, StringComparison.Ordinal))
        {
            return Invalid("plugin-identity-mismatch", markerCount, lifecycle, guid);
        }

        var api = values.GetValueOrDefault("api") ?? string.Empty;
        var contracts = values.GetValueOrDefault("contracts") ?? string.Empty;
        if (expectedApiIdentity is not null && !api.Equals(expectedApiIdentity, StringComparison.Ordinal)
            || expectedContractsIdentity is not null && !contracts.Equals(expectedContractsIdentity, StringComparison.Ordinal))
        {
            return Invalid("contract-identity-mismatch", markerCount, lifecycle, guid);
        }

        if (lifecycle)
        {
            return Invalid("lifecycle-marker", markerCount, true, guid);
        }

        return new(true, string.Empty, markerCount, false, guid, new SyntheticPluginMarker(nonce, guid, version, api, contracts));
    }

    private static PluginSmokeMarkerParseResult Invalid(string category, int count, bool lifecycle, string? guid)
        => new(false, category, count, lifecycle, guid, null);
}

public static class PluginDeploymentPath
{
    public static string GetPluginDirectory(string cleanGameRoot, string pluginGuid)
    {
        var canonicalGuid = CodeModBoundaryValueRules.NormalizeModId(pluginGuid);
        var root = Path.GetFullPath(cleanGameRoot);
        var path = Path.GetFullPath(Path.Combine(root, "BepInEx", "plugins", canonicalGuid));
        var relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new PluginSmokeException("The plugin deployment path is outside the disposable game profile.");
        }

        return path;
    }
}

public sealed record PluginSmokeRequest(
    string GamePath,
    string ExperimentRoot,
    string BepInExArchivePath,
    GameFingerprint ExpectedFingerprint,
    Sha256Digest ExpectedBepInExDigest,
    string RepositoryRoot)
{
    public string ToSanitizedString()
        => $"fingerprint={ExpectedFingerprint.Value};bepinex-digest={ExpectedBepInExDigest.Value};explicit-evidence=true";
}

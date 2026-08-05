using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ThroneForge.Contracts;
using ThroneForge.Runtime;

namespace ThroneForge.PluginSmokeTest;

public sealed class PluginSmokeException : Exception
{
    public PluginSmokeException(string message)
        : base(message)
    {
    }
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
        var files = relativePaths
            .Select(path =>
            {
                ValidateRelativePath(path);
                var fullPath = Path.GetFullPath(Path.Combine(packageRoot, path.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(fullPath))
                {
                    throw new PluginSmokeException("A declared synthetic plugin package file is missing.");
                }

                var metadata = PluginAssemblyMetadataInspector.Inspect(fullPath, path);
                if (!metadata.HasManagedMetadata)
                {
                    throw new PluginSmokeException("A declared synthetic plugin package file is not a managed assembly.");
                }

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
}

public sealed record PluginDeploymentPreconditions(
    bool LoaderTransactionApplied,
    bool CurrentManifestMatches,
    bool NoCustomPlugins,
    bool ProcessClosed);

public sealed record PluginDeploymentReceipt(
    string RelativeRoot,
    IReadOnlyList<string> DeployedRelativePaths,
    IReadOnlyList<string> DeployedSha256);

public static class PluginDeploymentService
{
    public static PluginDeploymentReceipt Deploy(
        string packageRoot,
        string cleanGameRoot,
        PluginPackageManifest package,
        PluginDeploymentPreconditions preconditions)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(preconditions);
        if (!preconditions.LoaderTransactionApplied || !preconditions.CurrentManifestMatches
            || !preconditions.NoCustomPlugins || !preconditions.ProcessClosed)
        {
            throw new PluginSmokeException("The disposable profile is not ready for synthetic plugin deployment.");
        }

        var directory = PluginDeploymentPath.GetPluginDirectory(cleanGameRoot, package.Identity.Id);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new PluginSmokeException("The synthetic plugin deployment directory is not empty.");
        }

        Directory.CreateDirectory(directory);
        var deployedPaths = new List<string>();
        var deployedHashes = new List<string>();
        foreach (var file in package.Files)
        {
            var source = Path.GetFullPath(Path.Combine(packageRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var destinationRelative = $"BepInEx/plugins/{package.Identity.Id}/{file.RelativePath}";
            var destination = Path.GetFullPath(Path.Combine(cleanGameRoot, destinationRelative.Replace('/', Path.DirectorySeparatorChar)));
            var relative = Path.GetRelativePath(cleanGameRoot, destination).Replace(Path.DirectorySeparatorChar, '/');
            if (!relative.Equals(destinationRelative, StringComparison.Ordinal)
                || File.Exists(destination)
                || Directory.Exists(destination))
            {
                throw new PluginSmokeException("The synthetic plugin deployment destination is unsafe or already occupied.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }

            var observed = PluginAssemblyMetadataInspector.Inspect(destination, destinationRelative);
            if (!observed.Sha256.Equals(file.Sha256))
            {
                throw new PluginSmokeException("A deployed synthetic plugin file did not match the approved package hash.");
            }

            deployedPaths.Add(destinationRelative);
            deployedHashes.Add(observed.Sha256.Value);
        }

        return new PluginDeploymentReceipt(
            $"BepInEx/plugins/{package.Identity.Id}",
            deployedPaths,
            deployedHashes);
    }

    public static void Remove(string cleanGameRoot, string pluginGuid)
    {
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
    IReadOnlyList<string> AssemblyReferences);

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
                references);
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
        var markerCount = lines.Count(line => line.Equals(ReadyMarker, StringComparison.Ordinal));
        var lifecycle = lines.Any(line => line.Equals(LifecycleMarker, StringComparison.Ordinal));
        if (markerCount != 1)
        {
            return Invalid("marker-count", markerCount, lifecycle, null);
        }

        var values = lines
            .Where(line => line.Contains('=', StringComparison.Ordinal))
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

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

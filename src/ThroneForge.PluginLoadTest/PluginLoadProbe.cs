using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using ThroneForge.API;
using ThroneForge.Contracts;
using ThroneForge.Runtime;

namespace ThroneForge.PluginLoadTest;

public static class PluginLoadProbe
{
    private const long MaximumArtifactBytes = 64 * 1024 * 1024;
    private const int UnloadObservationAttempts = 4;
    private static readonly AssemblyIdentity ApiIdentity = AssemblyIdentity.FromAssembly(typeof(IThroneForgeMod).Assembly.GetName());
    private static readonly AssemblyIdentity ContractsIdentity = AssemblyIdentity.FromAssembly(typeof(CodeModDescriptor).Assembly.GetName());
    private static readonly IReadOnlySet<string> TrustedPlatformAssemblyKeys = LoadTrustedPlatformAssemblyKeys();

    public static PluginArtifactCapture CaptureArtifact(string artifactPath)
    {
        var canonicalPath = Path.GetFullPath(artifactPath);
        using var stream = new FileStream(
            canonicalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);

        if (stream.Length < 1 || stream.Length > MaximumArtifactBytes)
        {
            throw new InvalidDataException("The plugin artifact exceeds the bounded load limit.");
        }

        using var buffered = new MemoryStream(checked((int)stream.Length));
        stream.CopyTo(buffered);
        if (buffered.Length != stream.Length)
        {
            throw new IOException("The plugin artifact changed while it was being captured.");
        }

        var bytes = buffered.ToArray();
        var digest = new Sha256Digest(Convert.ToHexString(SHA256.HashData(bytes)));
        return new PluginArtifactCapture(canonicalPath, bytes, digest);
    }

    public static PluginLoadResult Load(PluginLoadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ArtifactPath)
            || request.Descriptor is null
            || request.GameFingerprint is null
            || request.CompatibilityEvidence is null)
        {
            return Failed(PluginLoadReasonCodes.InvalidRequest, "The plugin-load request is malformed.");
        }

        PluginArtifactCapture capture;
        try
        {
            capture = CaptureArtifact(request.ArtifactPath);
        }
        catch (FileNotFoundException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnavailable, "The plugin artifact is unavailable.");
        }
        catch (DirectoryNotFoundException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnavailable, "The plugin artifact is unavailable.");
        }
        catch (ArgumentException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnavailable, "The plugin artifact is unavailable.");
        }
        catch (NotSupportedException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnavailable, "The plugin artifact is unavailable.");
        }
        catch (InvalidDataException exception) when (exception.Message.Contains("bounded", StringComparison.Ordinal))
        {
            return Failed(PluginLoadReasonCodes.ArtifactTooLarge, "The plugin artifact exceeds the bounded load limit.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnreadable, "The plugin artifact cannot be read.");
        }
        catch (IOException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnreadable, "The plugin artifact cannot be read.");
        }

        return Load(request, capture);
    }

    public static PluginLoadResult Load(PluginLoadRequest request, PluginArtifactCapture capture)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capture);

        if (string.IsNullOrWhiteSpace(request.ArtifactPath)
            || request.Descriptor is null
            || request.GameFingerprint is null
            || request.CompatibilityEvidence is null)
        {
            return Failed(PluginLoadReasonCodes.InvalidRequest, "The plugin-load request is malformed.");
        }

        try
        {
            var requestPath = Path.GetFullPath(request.ArtifactPath);
            if (!PathsEqual(requestPath, capture.CanonicalPath))
            {
                return Failed(PluginLoadReasonCodes.InvalidRequest, "The captured artifact does not match the requested artifact.");
            }
        }
        catch (ArgumentException)
        {
            return Failed(PluginLoadReasonCodes.InvalidRequest, "The plugin-load request is malformed.");
        }

        var capturedDigest = new Sha256Digest(Convert.ToHexString(SHA256.HashData(capture.Bytes.Span)));
        if (capturedDigest != capture.Sha256)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnreadable, "The captured plugin artifact is inconsistent.");
        }

        PluginAssemblyPreflight preflight;
        try
        {
            preflight = PluginAssemblyPreflight.Inspect(capture.Bytes, ApiIdentity, ContractsIdentity, TrustedPlatformAssemblyKeys);
        }
        catch (BadImageFormatException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact is not a valid managed assembly.");
        }
        catch (InvalidDataException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact does not contain readable managed metadata.");
        }
        catch (IOException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact metadata could not be inspected.");
        }

        var closureEvidence = preflight.ToClosureEvidence(capture.Sha256, ApiIdentity, ContractsIdentity);
        if (preflight.NativeDependenciesDetected)
        {
            return Rejected(
                PluginLoadReasonCodes.NativeDependencyNotAllowed,
                "The synthetic single-assembly probe rejects native dependencies during metadata preflight.",
                closureEvidence);
        }

        if (preflight.HasModuleInitializer)
        {
            return Rejected(
                PluginLoadReasonCodes.ModuleInitializerNotAllowed,
                "The synthetic artifact contains a module initializer and was rejected before load. Assembly loading remains full-trust.",
                closureEvidence);
        }

        if (preflight.NonPlatformAssemblyReferences.Count > 0)
        {
            return Rejected(
                PluginLoadReasonCodes.ManagedDependencyNotAllowed,
                "The synthetic single-assembly probe rejects unapproved managed dependencies and sidecar assemblies.",
                closureEvidence);
        }

        var integrityEvidence = new CodeModIntegrityEvidence(
            request.Descriptor.Identity,
            request.Descriptor.PackageSha256,
            capture.Sha256,
            CodeModIntegrityVerificationStatus.Verified,
            "sha256-file");
        var admissionRequest = new CodeModActivationRequest(
            request.Descriptor,
            request.GameFingerprint,
            integrityEvidence,
            request.Approval,
            request.CompatibilityEvidence);

        // Keep this gate immediately before the call path that invokes LoadFromStream.
        var admission = CodeModAdmissionGate.Evaluate(admissionRequest);
        if (admission.Status != CodeModAdmissionStatus.Approved)
        {
            return new PluginLoadResult(
                PluginLoadStatus.Rejected,
                admission.ReasonCode,
                admission.Message,
                admission.Binding,
                null,
                Array.Empty<string>(),
                PluginUnloadStatus.NotAttempted,
                false,
                closureEvidence,
                Array.Empty<string>());
        }

        WeakReference? loadContextReference = null;
        PluginUnloadStatus unloadStatus = PluginUnloadStatus.NotAttempted;
        try
        {
            var loaded = LoadAndInspect(capture.Bytes, ApiIdentity, ContractsIdentity, TrustedPlatformAssemblyKeys, out loadContextReference);
            unloadStatus = ObserveUnload(loadContextReference);
            if (unloadStatus != PluginUnloadStatus.UnloadObserved)
            {
                return new PluginLoadResult(
                    PluginLoadStatus.Failed,
                    PluginLoadReasonCodes.UnloadNotObserved,
                    "The collectible load context was unloaded by request but was not observed as collected within the bounded verification window.",
                    admission.Binding,
                    loaded.AssemblyName,
                    loaded.ImplementedContractTypes,
                    unloadStatus,
                    true,
                    closureEvidence,
                    loaded.ContractIssues);
            }

            if (loaded.ContractIssues.Count > 0)
            {
                return new PluginLoadResult(
                    PluginLoadStatus.Failed,
                    PluginLoadReasonCodes.ContractInvalid,
                    "The artifact contains an IThroneForgeMod type with an invalid public closed top-level shape.",
                    admission.Binding,
                    loaded.AssemblyName,
                    loaded.ImplementedContractTypes,
                    unloadStatus,
                    true,
                    closureEvidence,
                    loaded.ContractIssues);
            }

            if (loaded.ImplementedContractTypes.Count == 0)
            {
                return new PluginLoadResult(
                    PluginLoadStatus.Failed,
                    PluginLoadReasonCodes.ContractMissing,
                    "The loaded artifact does not expose an IThroneForgeMod implementation.",
                    admission.Binding,
                    loaded.AssemblyName,
                    Array.Empty<string>(),
                    unloadStatus,
                    true,
                    closureEvidence,
                    Array.Empty<string>());
            }

            if (loaded.ImplementedContractTypes.Count > 1)
            {
                return new PluginLoadResult(
                    PluginLoadStatus.Failed,
                    PluginLoadReasonCodes.ContractAmbiguous,
                    "The loaded artifact exposes multiple IThroneForgeMod implementations.",
                    admission.Binding,
                    loaded.AssemblyName,
                    loaded.ImplementedContractTypes,
                    unloadStatus,
                    true,
                    closureEvidence,
                    Array.Empty<string>());
            }

            return new PluginLoadResult(
                PluginLoadStatus.Loaded,
                CodeModAdmissionReasonCodes.Approved,
                "No plugin constructor or ThroneForge lifecycle method was explicitly invoked. The synthetic artifact was preflighted to reject module initializers. Assembly loading remains a full-trust operation.",
                admission.Binding,
                loaded.AssemblyName,
                loaded.ImplementedContractTypes,
                unloadStatus,
                true,
                closureEvidence,
                Array.Empty<string>());
        }
        catch (BadImageFormatException)
        {
            unloadStatus = ObserveIfRequested(loadContextReference, unloadStatus);
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact is not a valid managed assembly.", admission.Binding, unloadStatus, loadContextReference is not null, closureEvidence);
        }
        catch (FileLoadException)
        {
            unloadStatus = ObserveIfRequested(loadContextReference, unloadStatus);
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact could not be loaded.", admission.Binding, unloadStatus, loadContextReference is not null, closureEvidence);
        }
        catch (ReflectionTypeLoadException)
        {
            unloadStatus = ObserveIfRequested(loadContextReference, unloadStatus);
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact types could not be inspected.", admission.Binding, unloadStatus, loadContextReference is not null, closureEvidence);
        }
        catch (IOException)
        {
            unloadStatus = ObserveIfRequested(loadContextReference, unloadStatus);
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact could not be loaded.", admission.Binding, unloadStatus, loadContextReference is not null, closureEvidence);
        }
        catch (ArgumentException)
        {
            unloadStatus = ObserveIfRequested(loadContextReference, unloadStatus);
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact could not be loaded.", admission.Binding, unloadStatus, loadContextReference is not null, closureEvidence);
        }

        static PluginLoadResult Rejected(string reasonCode, string message, PluginLoadClosureEvidence evidence) =>
            new(PluginLoadStatus.Rejected, reasonCode, message, null, null, Array.Empty<string>(), PluginUnloadStatus.NotAttempted, false, evidence, Array.Empty<string>());

        static PluginLoadResult Failed(
            string reasonCode,
            string message,
            CodeModAdmissionBinding? binding = null,
            PluginUnloadStatus unloadStatus = PluginUnloadStatus.NotAttempted,
            bool unloadRequested = false,
            PluginLoadClosureEvidence? closureEvidence = null) =>
            new(PluginLoadStatus.Failed, reasonCode, message, binding, null, Array.Empty<string>(), unloadStatus, unloadRequested, closureEvidence, Array.Empty<string>());
    }

    private static PluginLoadResult Failed(
        string reasonCode,
        string message,
        CodeModAdmissionBinding? binding = null,
        PluginUnloadStatus unloadStatus = PluginUnloadStatus.NotAttempted,
        bool unloadRequested = false,
        PluginLoadClosureEvidence? closureEvidence = null) =>
        new(PluginLoadStatus.Failed, reasonCode, message, binding, null, Array.Empty<string>(), unloadStatus, unloadRequested, closureEvidence, Array.Empty<string>());

    public static PluginUnloadStatus ObserveUnload(WeakReference contextReference)
    {
        ArgumentNullException.ThrowIfNull(contextReference);

        for (var attempt = 0; attempt < UnloadObservationAttempts; attempt++)
        {
            if (!contextReference.IsAlive)
            {
                return PluginUnloadStatus.UnloadObserved;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return contextReference.IsAlive
            ? PluginUnloadStatus.UnloadNotObservedWithinBound
            : PluginUnloadStatus.UnloadObserved;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LoadedAssemblyFacts LoadAndInspect(
        ReadOnlyMemory<byte> artifactBytes,
        AssemblyIdentity apiIdentity,
        AssemblyIdentity contractsIdentity,
        IReadOnlySet<string> trustedPlatformAssemblyKeys,
        out WeakReference contextReference)
    {
        var loadContext = new ContractSharingLoadContext(apiIdentity, contractsIdentity, trustedPlatformAssemblyKeys);
        contextReference = new WeakReference(loadContext);
        try
        {
            using var assemblyStream = new MemoryStream(artifactBytes.ToArray(), writable: false);
            var assembly = loadContext.LoadFromStream(assemblyStream);
            var contractInspection = InspectContractTypes(assembly);
            return new LoadedAssemblyFacts(
                assembly.GetName().Name,
                contractInspection.ImplementedContractTypes,
                contractInspection.Issues);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static ContractInspection InspectContractTypes(Assembly assembly)
    {
        var contractTypes = assembly
            .GetTypes()
            .Where(type => typeof(IThroneForgeMod).IsAssignableFrom(type))
            .ToArray();
        var issues = contractTypes
            .Select(ClassifyContractType)
            .Where(issue => issue is not null)
            .Select(issue => issue!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var validTypes = contractTypes
            .Where(IsValidContractType)
            .Select(type => type.FullName)
            .Where(name => name is not null)
            .Select(name => name!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new ContractInspection(validTypes, issues, contractTypes.Length);
    }

    private static bool IsValidContractType(Type type) =>
        type.IsClass
        && type.IsPublic
        && !type.IsNested
        && !type.IsAbstract
        && !type.ContainsGenericParameters
        && typeof(IThroneForgeMod).IsAssignableFrom(type);

    private static string? ClassifyContractType(Type type)
    {
        if (IsValidContractType(type))
        {
            return null;
        }

        if (type.IsNested)
        {
            return PluginContractIssueCodes.Nested;
        }

        if (!type.IsPublic)
        {
            return PluginContractIssueCodes.Internal;
        }

        if (type.IsInterface)
        {
            return PluginContractIssueCodes.Interface;
        }

        if (type.IsAbstract)
        {
            return PluginContractIssueCodes.Abstract;
        }

        if (type.ContainsGenericParameters)
        {
            return PluginContractIssueCodes.OpenGeneric;
        }

        return PluginContractIssueCodes.Interface;
    }

    private static PluginUnloadStatus ObserveIfRequested(WeakReference? contextReference, PluginUnloadStatus current) =>
        contextReference is null || current == PluginUnloadStatus.UnloadObserved
            ? current
            : ObserveUnload(contextReference);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static HashSet<string> LoadTrustedPlatformAssemblyKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string paths)
        {
            return keys;
        }

        foreach (var path in paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                keys.Add(AssemblyIdentity.FromAssembly(AssemblyName.GetAssemblyName(path)).Key);
            }
            catch (FileNotFoundException)
            {
            }
            catch (BadImageFormatException)
            {
            }
            catch (IOException)
            {
            }
        }

        return keys;
    }

    private sealed record LoadedAssemblyFacts(
        string? AssemblyName,
        IReadOnlyList<string> ImplementedContractTypes,
        IReadOnlyList<string> ContractIssues);

    private sealed record ContractInspection(
        IReadOnlyList<string> ImplementedContractTypes,
        IReadOnlyList<string> Issues,
        int TotalContractTypes);

    private sealed class ContractSharingLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyIdentity apiIdentity;
        private readonly AssemblyIdentity contractsIdentity;
        private readonly IReadOnlySet<string> trustedPlatformAssemblyKeys;

        public ContractSharingLoadContext(
            AssemblyIdentity apiIdentity,
            AssemblyIdentity contractsIdentity,
            IReadOnlySet<string> trustedPlatformAssemblyKeys)
            : base(isCollectible: true)
        {
            this.apiIdentity = apiIdentity;
            this.contractsIdentity = contractsIdentity;
            this.trustedPlatformAssemblyKeys = trustedPlatformAssemblyKeys;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var identity = AssemblyIdentity.FromAssembly(assemblyName);
            if (identity.Key == apiIdentity.Key)
            {
                return typeof(IThroneForgeMod).Assembly;
            }

            if (identity.Key == contractsIdentity.Key)
            {
                return typeof(CodeModDescriptor).Assembly;
            }

            if (trustedPlatformAssemblyKeys.Contains(identity.Key))
            {
                return null;
            }

            throw new FileLoadException("The requested assembly is outside the approved single-assembly closure.");
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) =>
            throw new DllNotFoundException("Unmanaged dependencies are not allowed by the synthetic probe.");
    }

    private sealed record PluginAssemblyPreflight(
        string PrimaryAssemblyIdentity,
        IReadOnlyList<string> SharedAssemblyReferences,
        IReadOnlyList<string> TrustedPlatformAssemblyReferences,
        IReadOnlyList<string> NonPlatformAssemblyReferences,
        bool NativeDependenciesDetected,
        bool HasModuleInitializer)
    {
        public static PluginAssemblyPreflight Inspect(
            ReadOnlyMemory<byte> bytes,
            AssemblyIdentity apiIdentity,
            AssemblyIdentity contractsIdentity,
            IReadOnlySet<string> trustedPlatformAssemblyKeys)
        {
            using var stream = new MemoryStream(bytes.ToArray(), writable: false);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                throw new BadImageFormatException();
            }

            var metadata = peReader.GetMetadataReader();
            var definition = metadata.GetAssemblyDefinition();
            var assemblyIdentity = AssemblyIdentity.FromMetadataDefinition(metadata, definition);
            var shared = new List<string>();
            var trusted = new List<string>();
            var nonPlatform = new List<string>();

            foreach (var handle in metadata.AssemblyReferences)
            {
                var reference = AssemblyIdentity.FromMetadataReference(metadata, handle);
                if (reference.Key == apiIdentity.Key || reference.Key == contractsIdentity.Key)
                {
                    shared.Add(reference.Display);
                }
                else if (trustedPlatformAssemblyKeys.Contains(reference.Key))
                {
                    trusted.Add(reference.Display);
                }
                else
                {
                    nonPlatform.Add(reference.Display);
                }
            }

            return new PluginAssemblyPreflight(
                assemblyIdentity.Display,
                shared.Order(StringComparer.Ordinal).ToArray(),
                trusted.Order(StringComparer.Ordinal).ToArray(),
                nonPlatform.Order(StringComparer.Ordinal).ToArray(),
                (peReader.PEHeaders.CorHeader is null && peReader.PEHeaders.PEHeader?.ImportTableDirectory.Size > 0)
                    || metadata.GetTableRowCount(TableIndex.ImplMap) > 0,
                ContainsModuleInitializer(metadata));
        }

        public PluginLoadClosureEvidence ToClosureEvidence(
            Sha256Digest primaryArtifactSha256,
            AssemblyIdentity apiIdentity,
            AssemblyIdentity contractsIdentity) =>
            new(
                primaryArtifactSha256,
                new[] { apiIdentity.Display, contractsIdentity.Display },
                TrustedPlatformAssemblyReferences,
                NonPlatformAssemblyReferences,
                NativeDependenciesDetected);

        private static bool ContainsModuleInitializer(MetadataReader metadata)
        {
            foreach (var typeHandle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(typeHandle);
                if (metadata.GetString(type.Name) != "<Module>")
                {
                    continue;
                }

                foreach (var methodHandle in type.GetMethods())
                {
                    var method = metadata.GetMethodDefinition(methodHandle);
                    if (metadata.GetString(method.Name) == ".cctor"
                        && (method.Attributes & MethodAttributes.Static) != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private sealed record AssemblyIdentity(
        string Name,
        Version Version,
        string Culture,
        string PublicKeyToken)
    {
        public string Key => string.Join('|', Name, Version, Culture, PublicKeyToken);

        public string Display =>
            $"{Name}, Version={Version}, Culture={Culture}, PublicKeyToken={(PublicKeyToken.Length == 0 ? "null" : PublicKeyToken)}";

        public static AssemblyIdentity FromAssembly(AssemblyName assemblyName) =>
            new(
                assemblyName.Name ?? string.Empty,
                assemblyName.Version ?? new Version(0, 0, 0, 0),
                string.IsNullOrEmpty(assemblyName.CultureName) ? "neutral" : assemblyName.CultureName,
                ConvertPublicKeyToken(assemblyName.GetPublicKeyToken()));

        public static AssemblyIdentity FromMetadataDefinition(MetadataReader metadata, AssemblyDefinition definition) =>
            new(
                metadata.GetString(definition.Name),
                definition.Version,
                definition.Culture.IsNil ? "neutral" : metadata.GetString(definition.Culture),
                ConvertPublicKey(metadata.GetBlobBytes(definition.PublicKey), (definition.Flags & AssemblyFlags.PublicKey) != 0));

        public static AssemblyIdentity FromMetadataReference(MetadataReader metadata, AssemblyReferenceHandle handle)
        {
            var reference = metadata.GetAssemblyReference(handle);
            return new(
                metadata.GetString(reference.Name),
                reference.Version,
                reference.Culture.IsNil ? "neutral" : metadata.GetString(reference.Culture),
                ConvertPublicKey(metadata.GetBlobBytes(reference.PublicKeyOrToken), (reference.Flags & AssemblyFlags.PublicKey) != 0));
        }

        private static string ConvertPublicKeyToken(byte[]? value)
        {
            if (value is null || value.Length == 0)
            {
                return string.Empty;
            }

            return Convert.ToHexString(value).ToLowerInvariant();
        }

        private static string ConvertPublicKey(byte[] value, bool isFullPublicKey)
        {
            if (value.Length == 0)
            {
                return string.Empty;
            }

            if (!isFullPublicKey)
            {
                return Convert.ToHexString(value).ToLowerInvariant();
            }

            // Strong-name public-key tokens are defined by SHA-1; this is an identity encoding, not a security digest.
#pragma warning disable CA5350
            var hash = SHA1.HashData(value);
#pragma warning restore CA5350
            var token = hash[^8..].ToArray();
            Array.Reverse(token);
            return Convert.ToHexString(token).ToLowerInvariant();
        }
    }
}

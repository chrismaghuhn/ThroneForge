using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using ThroneForge.API;
using ThroneForge.Contracts;
using ThroneForge.Runtime;

namespace ThroneForge.PluginLoadTest;

public static class PluginLoadProbe
{
    private const long MaximumArtifactBytes = 64 * 1024 * 1024;

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

        Sha256Digest observedPackageSha256;
        byte[] artifactBytes;
        try
        {
            using var stream = new FileStream(
                request.ArtifactPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);

            if (stream.Length < 1 || stream.Length > MaximumArtifactBytes)
            {
                return Failed(PluginLoadReasonCodes.ArtifactTooLarge, "The plugin artifact exceeds the bounded load limit.");
            }

            using var buffered = new MemoryStream(checked((int)stream.Length));
            stream.CopyTo(buffered);
            artifactBytes = buffered.ToArray();
            observedPackageSha256 = new Sha256Digest(Convert.ToHexString(SHA256.HashData(artifactBytes)));
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
        catch (UnauthorizedAccessException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnreadable, "The plugin artifact cannot be read.");
        }
        catch (IOException)
        {
            return Failed(PluginLoadReasonCodes.ArtifactUnreadable, "The plugin artifact cannot be read.");
        }

        var integrityEvidence = new CodeModIntegrityEvidence(
            request.Descriptor.Identity,
            request.Descriptor.PackageSha256,
            observedPackageSha256,
            CodeModIntegrityVerificationStatus.Verified,
            "sha256-file");
        var admissionRequest = new CodeModActivationRequest(
            request.Descriptor,
            request.GameFingerprint,
            integrityEvidence,
            request.Approval,
            request.CompatibilityEvidence);

        // This is intentionally the final decision immediately before the load call.
        var admission = CodeModAdmissionGate.Evaluate(admissionRequest);
        if (admission.Status != CodeModAdmissionStatus.Approved)
        {
            return new PluginLoadResult(
                PluginLoadStatus.Rejected,
                admission.ReasonCode,
                admission.Message,
                admission.Binding,
                null,
                Array.Empty<string>());
        }

        try
        {
            var loadContext = new ContractSharingLoadContext(request.ArtifactPath);
            try
            {
                using var assemblyStream = new MemoryStream(artifactBytes, writable: false);
                var assembly = loadContext.LoadFromStream(assemblyStream);
                var contractTypes = assembly
                    .GetTypes()
                    .Where(type => typeof(IThroneForgeMod).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                    .Select(type => type.FullName)
                    .Where(name => name is not null)
                    .Select(name => name!)
                    .Order(StringComparer.Ordinal)
                    .ToArray();

                if (contractTypes.Length == 0)
                {
                    return new PluginLoadResult(
                        PluginLoadStatus.Failed,
                        PluginLoadReasonCodes.ContractMissing,
                        "The loaded artifact does not expose an IThroneForgeMod implementation.",
                        admission.Binding,
                        assembly.GetName().Name,
                        Array.Empty<string>());
                }

                if (contractTypes.Length > 1)
                {
                    return new PluginLoadResult(
                        PluginLoadStatus.Failed,
                        PluginLoadReasonCodes.ContractAmbiguous,
                        "The loaded artifact exposes multiple IThroneForgeMod implementations.",
                        admission.Binding,
                        assembly.GetName().Name,
                        contractTypes);
                }

                return new PluginLoadResult(
                    PluginLoadStatus.Loaded,
                    CodeModAdmissionReasonCodes.Approved,
                    "The exact admitted synthetic artifact was loaded without invoking plugin code.",
                    admission.Binding,
                    assembly.GetName().Name,
                    contractTypes);
            }
            finally
            {
                loadContext.Unload();
            }
        }
        catch (BadImageFormatException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact is not a valid managed assembly.", admission.Binding);
        }
        catch (FileLoadException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact could not be loaded.", admission.Binding);
        }
        catch (ReflectionTypeLoadException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact types could not be inspected.", admission.Binding);
        }
        catch (IOException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact could not be loaded.", admission.Binding);
        }
        catch (ArgumentException)
        {
            return Failed(PluginLoadReasonCodes.AssemblyLoadFailed, "The plugin artifact could not be loaded.", admission.Binding);
        }

        static PluginLoadResult Failed(string reasonCode, string message, CodeModAdmissionBinding? binding = null) =>
            new(PluginLoadStatus.Failed, reasonCode, message, binding, null, Array.Empty<string>());
    }

    private sealed class ContractSharingLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public ContractSharingLoadContext(string artifactPath)
            : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(Path.GetFullPath(artifactPath));
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, typeof(IThroneForgeMod).Assembly.GetName().Name, StringComparison.Ordinal))
            {
                return typeof(IThroneForgeMod).Assembly;
            }

            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return resolvedPath is null ? null : LoadFromAssemblyPath(resolvedPath);
        }
    }
}

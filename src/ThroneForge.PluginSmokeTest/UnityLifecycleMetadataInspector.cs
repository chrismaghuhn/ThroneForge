using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
namespace ThroneForge.PluginSmokeTest;

public static class UnityLifecycleMetadataInspector
{
    public const long DefaultMaximumBytes = 64 * 1024 * 1024;

    public static UnityLifecycleMetadataResult Inspect(string filePath, long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (maximumBytes < 1)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1, nameof(maximumBytes));
        }

        try
        {
            using var input = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            if (input.Length > maximumBytes)
            {
                return Invalid("unity-metadata-too-large");
            }

            using var capture = new MemoryStream(checked((int)input.Length));
            input.CopyTo(capture);
            return InspectBytes(capture.ToArray(), maximumBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Invalid("unity-metadata-unreadable");
        }
    }

    public static UnityLifecycleMetadataResult InspectBytes(byte[] bytes, long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.LongLength > maximumBytes)
        {
            return Invalid("unity-metadata-too-large");
        }

        try
        {
            using var peReader = new PEReader(new MemoryStream(bytes, writable: false));
            if (!peReader.HasMetadata)
            {
                return Invalid("unity-metadata-missing");
            }

            var metadata = peReader.GetMetadataReader();
            if (!metadata.IsAssembly)
            {
                return Invalid("unity-assembly-identity-missing");
            }

            var assembly = metadata.GetAssemblyDefinition();
            var assemblyIdentity = $"{metadata.GetString(assembly.Name)}, Version={assembly.Version}";
            var applicationTypes = metadata.TypeDefinitions
                .Where(handle => IsApplicationType(metadata, handle))
                .ToArray();
            if (applicationTypes.Length != 1)
            {
                return Invalid(applicationTypes.Length == 0 ? "unity-application-type-missing" : "unity-application-type-ambiguous", assemblyIdentity);
            }

            var application = metadata.GetTypeDefinition(applicationTypes[0]);
            var quittingEvents = application.GetEvents()
                .Where(handle => metadata.GetString(metadata.GetEventDefinition(handle).Name) == "quitting")
                .ToArray();
            if (quittingEvents.Length != 1)
            {
                return Invalid(quittingEvents.Length == 0 ? "unity-quitting-event-missing" : "unity-quitting-event-ambiguous", assemblyIdentity);
            }

            var eventDefinition = metadata.GetEventDefinition(quittingEvents[0]);
            var handlerType = GetTypeName(metadata, eventDefinition.Type);
            var accessors = eventDefinition.GetAccessors();
            var adder = accessors.Adder.IsNil ? default : metadata.GetMethodDefinition(accessors.Adder);
            var remover = accessors.Remover.IsNil ? default : metadata.GetMethodDefinition(accessors.Remover);
            var valid = handlerType == "System.Action"
                && !accessors.Adder.IsNil
                && !accessors.Remover.IsNil
                && IsPublicStatic(adder.Attributes)
                && IsPublicStatic(remover.Attributes);

            return new(
                valid,
                LifecycleBindingIds.ApplicationQuittingV1,
                valid ? null : "unity-quitting-event-invalid",
                assemblyIdentity,
                "UnityEngine.Application",
                "quitting",
                handlerType);
        }
        catch (BadImageFormatException)
        {
            return Invalid("unity-metadata-malformed");
        }
        catch (ArgumentException)
        {
            return Invalid("unity-metadata-malformed");
        }
    }

    private static bool IsApplicationType(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var type = metadata.GetTypeDefinition(handle);
        return metadata.GetString(type.Namespace) == "UnityEngine"
            && metadata.GetString(type.Name) == "Application"
            && !type.Attributes.HasFlag(TypeAttributes.NestedPublic);
    }

    private static bool IsPublicStatic(MethodAttributes attributes)
        => attributes.HasFlag(MethodAttributes.Public) && attributes.HasFlag(MethodAttributes.Static);

    private static string GetTypeName(MetadataReader metadata, EntityHandle handle)
        => handle.Kind switch
        {
            HandleKind.TypeReference => GetTypeReferenceName(metadata, (TypeReferenceHandle)handle),
            HandleKind.TypeDefinition => GetTypeDefinitionName(metadata, (TypeDefinitionHandle)handle),
            _ => string.Empty
        };

    private static string GetTypeReferenceName(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var type = metadata.GetTypeReference(handle);
        var namespaceName = metadata.GetString(type.Namespace);
        var name = metadata.GetString(type.Name);
        return namespaceName.Length == 0 ? name : $"{namespaceName}.{name}";
    }

    private static string GetTypeDefinitionName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var type = metadata.GetTypeDefinition(handle);
        var namespaceName = metadata.GetString(type.Namespace);
        var name = metadata.GetString(type.Name);
        return namespaceName.Length == 0 ? name : $"{namespaceName}.{name}";
    }

    private static UnityLifecycleMetadataResult Invalid(string category, string? assemblyIdentity = null)
        => new(false, LifecycleBindingIds.ApplicationQuittingV1, category, assemblyIdentity, "UnityEngine.Application", "quitting", null);
}

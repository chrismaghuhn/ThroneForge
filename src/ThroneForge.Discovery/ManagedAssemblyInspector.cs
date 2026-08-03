using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ThroneForge.Discovery;

public static class ManagedAssemblyInspector
{
    private const long MaximumManagedAssemblyBytes = 16 * 1024 * 1024;

    public static bool TryInspect(
        string path,
        string relativePath,
        out ManagedAssemblyEvidence evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        evidence = new ManagedAssemblyEvidence(
            relativePath,
            HasManagedMetadata: false,
            AssemblyName: null,
            AssemblyVersion: null,
            TargetFramework: null,
            SelectedFrameworkReferences: [],
            FailureReason: null);

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.SequentialScan);
            if (stream.Length > MaximumManagedAssemblyBytes)
            {
                evidence = evidence with { FailureReason = "Candidate exceeded the bounded metadata size limit." };
                return false;
            }

            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
            if (!peReader.HasMetadata)
            {
                evidence = evidence with { FailureReason = "Candidate does not contain managed metadata." };
                return false;
            }

            var metadataReader = peReader.GetMetadataReader();
            var assembly = metadataReader.GetAssemblyDefinition();
            var assemblyName = metadataReader.GetString(assembly.Name);
            var targetFramework = ReadTargetFramework(metadataReader, assembly);
            var selectedReferences = ReadSelectedFrameworkReferences(metadataReader, assembly);
            evidence = new ManagedAssemblyEvidence(
                relativePath,
                HasManagedMetadata: true,
                assemblyName,
                assembly.Version,
                targetFramework,
                selectedReferences,
                FailureReason: null);
            return true;
        }
        catch (BadImageFormatException)
        {
            evidence = evidence with { FailureReason = "Candidate has malformed managed metadata." };
            return false;
        }
        catch (InvalidOperationException)
        {
            evidence = evidence with { FailureReason = "Candidate has unreadable managed metadata." };
            return false;
        }
        catch (IOException)
        {
            evidence = evidence with { FailureReason = "Candidate could not be read." };
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            evidence = evidence with { FailureReason = "Candidate could not be read." };
            return false;
        }
        catch (ArgumentException)
        {
            evidence = evidence with { FailureReason = "Candidate path is not accessible." };
            return false;
        }
    }

    private static FrameworkAssemblyReference[] ReadSelectedFrameworkReferences(
        MetadataReader reader,
        AssemblyDefinition assembly)
    {
        var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mscorlib",
            "netstandard",
            "System",
            "System.Core",
            "System.Runtime"
        };

        return reader.AssemblyReferences
            .Select(reader.GetAssemblyReference)
            .Where(reference => selectedNames.Contains(reader.GetString(reference.Name)))
            .Select(reference => new FrameworkAssemblyReference(
                reader.GetString(reference.Name),
                reference.Version))
            .OrderBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.Version)
            .ToArray();
    }

    private static string? ReadTargetFramework(MetadataReader reader, AssemblyDefinition assembly)
    {
        foreach (var attributeHandle in assembly.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            if (!string.Equals(
                    GetAttributeTypeName(reader, attribute.Constructor),
                    "System.Runtime.Versioning.TargetFrameworkAttribute",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var blobReader = reader.GetBlobReader(attribute.Value);
            if (blobReader.ReadUInt16() != 1)
            {
                return null;
            }

            return blobReader.ReadSerializedString();
        }

        return null;
    }

    private static string? GetAttributeTypeName(MetadataReader reader, EntityHandle constructor)
    {
        EntityHandle declaringType = constructor.Kind switch
        {
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            _ => default
        };

        return declaringType.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeDefinitionName(reader, reader.GetTypeDefinition((TypeDefinitionHandle)declaringType)),
            HandleKind.TypeReference => GetTypeReferenceName(reader, reader.GetTypeReference((TypeReferenceHandle)declaringType)),
            _ => null
        };
    }

    private static string GetTypeDefinitionName(MetadataReader reader, TypeDefinition definition)
    {
        var name = reader.GetString(definition.Name);
        var @namespace = reader.GetString(definition.Namespace);
        return string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}";
    }

    private static string GetTypeReferenceName(MetadataReader reader, TypeReference reference)
    {
        var name = reader.GetString(reference.Name);
        var @namespace = reader.GetString(reference.Namespace);
        return string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}";
    }
}

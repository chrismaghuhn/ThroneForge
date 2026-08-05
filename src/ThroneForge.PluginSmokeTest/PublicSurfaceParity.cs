using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ThroneForge.PluginSmokeTest;

public sealed record PublicSurfaceSnapshot(IReadOnlyList<string> Members)
{
    public string CanonicalText => string.Join("\n", Members);
}

public static class PublicSurfaceParityService
{
    public static PublicSurfaceSnapshot Capture(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                throw new PluginSmokeException("The public-surface input has no managed metadata.");
            }

            var metadata = peReader.GetMetadataReader();
            var members = new List<string>();
            foreach (var handle in metadata.TypeDefinitions)
            {
                var type = metadata.GetTypeDefinition(handle);
                if (!type.Attributes.HasFlag(TypeAttributes.Public)
                    || type.Attributes.HasFlag(TypeAttributes.NestedPublic))
                {
                    continue;
                }

                var typeName = metadata.GetString(type.Name);
                var kind = type.Attributes.HasFlag(TypeAttributes.Interface) ? "interface"
                    : type.BaseType.Kind == HandleKind.TypeReference
                        && metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)type.BaseType).Name) == "Enum"
                        ? "enum"
                        : "type";
                members.Add($"type|{typeName}|{kind}|base={TypeName(metadata, type.BaseType)}");
                foreach (var implementation in type.GetInterfaceImplementations()
                             .Select(item => TypeName(metadata, metadata.GetInterfaceImplementation(item).Interface))
                             .Order(StringComparer.Ordinal))
                {
                    members.Add($"interface|{typeName}|{implementation}");
                }

                foreach (var methodHandle in type.GetMethods())
                {
                    var method = metadata.GetMethodDefinition(methodHandle);
                    if (!method.Attributes.HasFlag(MethodAttributes.Public))
                    {
                        continue;
                    }

                    var signature = method.DecodeSignature(new MetadataTypeNameProvider(metadata), genericContext: null);
                    members.Add($"method|{typeName}|{metadata.GetString(method.Name)}|return={signature.ReturnType}|parameters={string.Join(',', signature.ParameterTypes)}");
                }

                foreach (var propertyHandle in type.GetProperties())
                {
                    var property = metadata.GetPropertyDefinition(propertyHandle);
                    var accessors = property.GetAccessors();
                    if (!IsPublicAccessor(metadata, accessors.Getter) && !IsPublicAccessor(metadata, accessors.Setter))
                    {
                        continue;
                    }

                    var signature = property.DecodeSignature(new MetadataTypeNameProvider(metadata), genericContext: null);
                    members.Add($"property|{typeName}|{metadata.GetString(property.Name)}|type={signature.ReturnType}|parameters={string.Join(',', signature.ParameterTypes)}");
                }

                foreach (var fieldHandle in type.GetFields())
                {
                    var field = metadata.GetFieldDefinition(fieldHandle);
                    if (!field.Attributes.HasFlag(FieldAttributes.Public))
                    {
                        continue;
                    }

                    var fieldType = field.DecodeSignature(new MetadataTypeNameProvider(metadata), genericContext: null);
                    members.Add($"field|{typeName}|{metadata.GetString(field.Name)}|{fieldType}");
                }
            }

            return new PublicSurfaceSnapshot(members.Order(StringComparer.Ordinal).ToArray());
        }
        catch (PluginSmokeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
        {
            throw new PluginSmokeException("The public assembly surface could not be inspected safely.", exception);
        }
    }

    public static void RequireEquivalent(string firstPath, string secondPath)
    {
        var first = Capture(firstPath);
        var second = Capture(secondPath);
        if (!first.Members.SequenceEqual(second.Members, StringComparer.Ordinal))
        {
            var differences = first.Members.Except(second.Members, StringComparer.Ordinal)
                .Concat(second.Members.Except(first.Members, StringComparer.Ordinal))
                .Take(8);
            throw new PluginSmokeException("The public API surfaces differ between the selected target frameworks: " + string.Join("; ", differences));
        }
    }

    private static bool IsPublicAccessor(MetadataReader metadata, MethodDefinitionHandle handle)
        => !handle.IsNil && metadata.GetMethodDefinition(handle).Attributes.HasFlag(MethodAttributes.Public);

    private sealed class MetadataTypeNameProvider : ISignatureTypeProvider<string, object?>
    {
        private readonly MetadataReader _metadata;

        public MetadataTypeNameProvider(MetadataReader metadata) => _metadata = metadata;

        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";
        public string GetByReferenceType(string elementType) => $"ref {elementType}";
        public string GetFunctionPointerType(MethodSignature<string> signature) => $"fn({signature.ReturnType} {string.Join(',', signature.ParameterTypes)})";
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(',', typeArguments)}>`";
        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
        public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetPinnedType(string elementType) => $"pinned {elementType}";
        public string GetPointerType(string elementType) => $"{elementType}*";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSZArrayType(string elementType) => $"{elementType}[]";
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            => TypeName(reader, handle);
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            => TypeName(reader, handle);
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        private static string TypeName(MetadataReader metadata, EntityHandle handle)
            => handle.Kind switch
            {
                HandleKind.TypeReference => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)handle).Name),
                HandleKind.TypeDefinition => metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
                _ => string.Empty
            };
    }

    private static string TypeName(MetadataReader metadata, EntityHandle handle)
    {
        try
        {
            return handle.Kind switch
            {
                HandleKind.TypeReference => metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)handle).Name),
                HandleKind.TypeDefinition => metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
                _ => string.Empty
            };
        }
        catch (BadImageFormatException)
        {
            return "<invalid-metadata-type>";
        }
    }
}

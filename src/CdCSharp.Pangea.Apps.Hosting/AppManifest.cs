using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace CdCSharp.Pangea.Apps.Hosting;

/// <summary>What an application says about itself, read from its assembly.</summary>
/// <param name="Id">Its <see cref="PangeaAppAttribute.Id"/>.</param>
/// <param name="Name">Its <see cref="PangeaAppAttribute.Name"/>.</param>
/// <param name="AssemblyPath">The assembly it was read from.</param>
public sealed record AppManifest(string Id, string Name, string AssemblyPath)
{
    public string? Description { get; init; }

    /// <summary>The icon file's full path, when the application has one and it is there.</summary>
    public string? IconPath { get; init; }

    public string? Version { get; init; }

    public string? Commit { get; init; }

    /// <summary>The message ids it answers.</summary>
    public IReadOnlyList<string> Handles { get; init; } = [];
}

/// <summary>
/// Reads an application's manifest from its assembly's metadata, without loading the assembly or
/// running a line of it.
/// </summary>
/// <remarks>
/// A host lists, updates and starts applications it knows nothing else about - eventually ones it
/// fetched from somewhere - so what it learns before deciding to run one has to come from data.
/// The metadata reader parses the file; nothing in it executes.
/// </remarks>
public static class AppManifestReader
{
    private const string AppAttribute = "CdCSharp.Pangea.Apps.PangeaAppAttribute";
    private const string HandlesAttribute = "CdCSharp.Pangea.Apps.PangeaAppHandlesAttribute";
    private const string VersionAttribute = "System.Reflection.AssemblyInformationalVersionAttribute";

    /// <returns>The manifest, or null when the file is not a Pangea application.</returns>
    public static AppManifest? TryRead(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath)) return null;

        try
        {
            using FileStream stream = File.OpenRead(assemblyPath);
            using PEReader pe = new(stream);

            if (!pe.HasMetadata) return null;

            MetadataReader metadata = pe.GetMetadataReader();

            if (!metadata.IsAssembly) return null;

            string? id = null, name = null, description = null, icon = null, informational = null;
            List<string> handles = [];

            foreach (CustomAttributeHandle handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                CustomAttribute attribute = metadata.GetCustomAttribute(handle);
                string? type = AttributeTypeName(metadata, attribute);

                if (type is not (AppAttribute or HandlesAttribute or VersionAttribute)) continue;

                CustomAttributeValue<string> value = attribute.DecodeValue(StringTypeProvider.Instance);
                ImmutableArray<CustomAttributeTypedArgument<string>> arguments = value.FixedArguments;

                switch (type)
                {
                    case AppAttribute when arguments.Length >= 2:
                        id = arguments[0].Value as string;
                        name = arguments[1].Value as string;

                        foreach (CustomAttributeNamedArgument<string> named in value.NamedArguments)
                        {
                            if (named.Name == "Description") description = named.Value as string;
                            else if (named.Name == "Icon") icon = named.Value as string;
                        }

                        break;

                    case HandlesAttribute when arguments.Length >= 1 && arguments[0].Value is string message:
                        handles.Add(message);
                        break;

                    case VersionAttribute when arguments.Length >= 1:
                        informational = arguments[0].Value as string;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(id)) return null;

            (string? version, string? commit) = SplitVersion(informational);
            string? folder = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
            string? iconPath = icon is null || folder is null ? null : Path.Combine(folder, icon);

            return new AppManifest(id, string.IsNullOrWhiteSpace(name) ? id : name, Path.GetFullPath(assemblyPath))
            {
                Description = description,
                IconPath = iconPath is not null && File.Exists(iconPath) ? iconPath : null,
                Version = version,
                Commit = commit,
                Handles = handles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            };
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Splits <c>1.2.3+abcdef</c> into the version and the commit the SDK appends.</summary>
    public static (string? Version, string? Commit) SplitVersion(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational)) return (null, null);

        int plus = informational.IndexOf('+');

        return plus < 0
            ? (informational, null)
            : (informational[..plus], informational[(plus + 1)..] is { Length: > 0 } commit ? commit : null);
    }

    private static string? AttributeTypeName(MetadataReader metadata, CustomAttribute attribute)
    {
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                MemberReference member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);

                return member.Parent.Kind == HandleKind.TypeReference
                    ? FullName(metadata, metadata.GetTypeReference((TypeReferenceHandle)member.Parent))
                    : null;

            case HandleKind.MethodDefinition:
                MethodDefinition method = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                TypeDefinition declaring = metadata.GetTypeDefinition(method.GetDeclaringType());

                return $"{metadata.GetString(declaring.Namespace)}.{metadata.GetString(declaring.Name)}";

            default:
                return null;
        }
    }

    private static string FullName(MetadataReader metadata, TypeReference type) =>
        $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";

    /// <summary>Enough of a type provider to decode attributes whose arguments are strings and primitives.</summary>
    private sealed class StringTypeProvider : ICustomAttributeTypeProvider<string>
    {
        public static StringTypeProvider Instance { get; } = new();

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        public string GetSystemType() => "System.Type";

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            reader.GetString(reader.GetTypeDefinition(handle).Name);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            reader.GetString(reader.GetTypeReference(handle).Name);

        public string GetTypeFromSerializedName(string name) => name;

        // Every enum in an attribute this reader cares about is an int; one that is not is skipped
        // by the caller, which only reads the attributes above.
        public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;

        public bool IsSystemType(string type) => type == "System.Type";
    }
}

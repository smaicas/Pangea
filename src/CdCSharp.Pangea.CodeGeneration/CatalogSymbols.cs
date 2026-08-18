using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CdCSharp.Pangea.CodeGeneration;

/// <summary>The toolkit types a catalog is built by recognising, resolved once per compilation.</summary>
internal sealed class CatalogTypes
{
    internal const string CatalogNamespace = "CdCSharp.Pangea.Generated";
    internal const string CatalogTypeName = "PangeaCatalog";

    private CatalogTypes(
        INamedTypeSymbol? feature,
        INamedTypeSymbol? viewModelBase,
        INamedTypeSymbol? control,
        INamedTypeSymbol? navigationRequest)
    {
        Feature = feature;
        ViewModelBase = viewModelBase;
        Control = control;
        NavigationRequest = navigationRequest;
    }

    internal INamedTypeSymbol? Feature { get; }

    internal INamedTypeSymbol? ViewModelBase { get; }

    /// <summary>Null when the project does not reference Avalonia, which a library of view models need not.</summary>
    internal INamedTypeSymbol? Control { get; }

    /// <summary>The open generic <c>INavigationRequest&lt;&gt;</c>.</summary>
    internal INamedTypeSymbol? NavigationRequest { get; }

    /// <summary>Whether the toolkit is referenced at all. Nothing is generated when it is not.</summary>
    internal bool IsPangeaProject => Feature is not null && ViewModelBase is not null;

    internal static CatalogTypes From(Compilation compilation) =>
        new(compilation.GetTypeByMetadataName("CdCSharp.Pangea.Core.Abstractions.IPangeaFeature"),
            compilation.GetTypeByMetadataName("CdCSharp.Pangea.Core.Base.ViewModelBase"),
            compilation.GetTypeByMetadataName("Avalonia.Controls.Control"),
            compilation.GetTypeByMetadataName("CdCSharp.Pangea.Core.Abstractions.INavigationRequest`1"));
}

/// <summary>
/// Reads a type and decides what, if anything, it contributes to the catalog.
/// </summary>
/// <remarks>
/// Every rule here mirrors one the toolkit applies at runtime by scanning: what
/// <c>FeatureRegistry</c> instantiates, what <c>AppBuilderExtensions</c> registers, what
/// <c>ViewLocator</c> looks up by name, and what <c>NavigationFeature</c> verifies. They have to
/// agree, or the generated catalog describes a different application from the one that runs.
/// </remarks>
internal static class CatalogSymbols
{
    private static readonly SymbolDisplayFormat FullyQualified =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    internal static string FullName(ITypeSymbol type) => type.ToDisplayString(FullyQualified);

    /// <summary>A type nothing can instantiate contributes nothing, whatever it inherits.</summary>
    internal static bool IsInstantiable(INamedTypeSymbol type) =>
        type is { IsAbstract: false, IsStatic: false, IsGenericType: false, TypeKind: TypeKind.Class } &&
        type.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;

    internal static bool Implements(INamedTypeSymbol type, INamedTypeSymbol? contract) =>
        contract is not null &&
        type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, contract));

    internal static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol? baseType)
    {
        if (baseType is null) return false;

        for (INamedTypeSymbol? candidate = type.BaseType; candidate is not null; candidate = candidate.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, baseType)) return true;
        }

        return false;
    }

    internal static bool HasPublicParameterlessConstructor(INamedTypeSymbol type) =>
        type.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0 &&
            constructor.DeclaredAccessibility == Accessibility.Public);

    /// <summary>
    /// The expression that builds <paramref name="type"/> from a service provider called <c>sp</c>.
    /// </summary>
    /// <remarks>
    /// A single constructor whose parameters are all services is written out as a <c>new</c>, which
    /// is the whole point: no reflection, and a constructor the trimmer can see is referenced.
    /// Anything less obvious - several constructors to choose between, optional parameters, a
    /// primitive nothing registers - is handed to <c>ActivatorUtilities</c>, which is what the
    /// container would have done anyway.
    /// </remarks>
    internal static string BuildExpression(INamedTypeSymbol type)
    {
        string name = FullName(type);

        List<IMethodSymbol> constructors = type.InstanceConstructors
            .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        if (constructors.Count != 1) return Activated(name);

        IMethodSymbol chosen = constructors[0];

        if (chosen.Parameters.Any(parameter =>
                parameter.IsOptional || parameter.IsParams || parameter.RefKind != RefKind.None ||
                !IsResolvable(parameter.Type)))
        {
            return Activated(name);
        }

        StringBuilder arguments = new();

        for (int index = 0; index < chosen.Parameters.Length; index++)
        {
            if (index > 0) arguments.Append(", ");

            IParameterSymbol parameter = chosen.Parameters[index];

            arguments.Append(IsServiceProvider(parameter.Type)
                ? "sp"
                : $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{FullName(parameter.Type)}>(sp)");
        }

        return $"new {name}({arguments})";
    }

    private static string Activated(string name) =>
        $"global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(sp, typeof({name}))";

    private static bool IsServiceProvider(ITypeSymbol type) => type.SpecialType == SpecialType.None &&
        type.ToDisplayString() == "System.IServiceProvider";

    /// <summary>
    /// Whether a container could plausibly supply this. A string or an int is a configuration
    /// value, not a service, and asking for one by type is how a startup failure is produced.
    /// </summary>
    private static bool IsResolvable(ITypeSymbol type) =>
        type.TypeKind is TypeKind.Interface or TypeKind.Class &&
        type.SpecialType == SpecialType.None;

    /// <summary>The assembly name as a C# identifier, for the namespace the catalog lives in.</summary>
    internal static string Identifier(string assemblyName)
    {
        StringBuilder identifier = new(assemblyName.Length);

        foreach (char character in assemblyName)
        {
            identifier.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        if (identifier.Length == 0 || char.IsDigit(identifier[0])) identifier.Insert(0, '_');

        return identifier.ToString();
    }

    /// <summary>The destination each <c>INavigationRequest&lt;T&gt;</c> the type declares points at.</summary>
    internal static IEnumerable<ITypeSymbol> NavigationDestinations(INamedTypeSymbol type, INamedTypeSymbol? request)
    {
        if (request is null) yield break;

        foreach (INamedTypeSymbol contract in type.AllInterfaces)
        {
            if (contract.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, request))
            {
                yield return contract.TypeArguments[0];
            }
        }
    }
}

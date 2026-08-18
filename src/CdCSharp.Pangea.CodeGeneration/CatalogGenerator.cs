using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace CdCSharp.Pangea.CodeGeneration;

/// <summary>
/// Writes out what startup would otherwise discover by scanning assemblies.
/// </summary>
/// <remarks>
/// <para>
/// A Pangea application starts by walking every assembly it can reach and reading every type in it,
/// to answer four questions: which classes are features, which are view models, which controls
/// display them, and which navigation requests exist. All four are answerable while the code is
/// being compiled, and the answers do not change afterwards.
/// </para>
/// <para>
/// So this emits them, as a class the runtime reads instead of scanning. The scan stays as the
/// fallback for a project the generator never ran in.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class CatalogGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol?> declarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (syntax, cancellation) =>
                    syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation) as INamedTypeSymbol);

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<INamedTypeSymbol?> Types)> everything =
            context.CompilationProvider.Combine(declarations.Collect());

        context.RegisterSourceOutput(everything, static (production, input) =>
        {
            CatalogModel? model = Build(input.Compilation, input.Types, production.CancellationToken);

            if (model is null) return;

            production.AddSource("PangeaCatalog.g.cs", CatalogWriter.Write(model));
        });
    }

    private static CatalogModel? Build(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol?> declared,
        System.Threading.CancellationToken cancellation)
    {
        CatalogTypes types = CatalogTypes.From(compilation);

        // Nothing to catalogue in a project that does not reference the toolkit.
        if (!types.IsPangeaProject) return null;

        List<FeatureModel> features = new();
        List<ViewModelModel> viewModels = new();
        List<ViewEntryModel> views = new();
        List<NavigationModel> navigationRequests = new();

        HashSet<string> seen = new();

        foreach (INamedTypeSymbol? type in declared)
        {
            cancellation.ThrowIfCancellationRequested();

            if (type is null || !CatalogSymbols.IsInstantiable(type)) continue;

            // A partial type is declared more than once and would otherwise be catalogued twice.
            string name = CatalogSymbols.FullName(type);
            if (!seen.Add(name)) continue;

            if (CatalogSymbols.Implements(type, types.Feature) &&
                CatalogSymbols.HasPublicParameterlessConstructor(type))
            {
                features.Add(new FeatureModel(name));
            }

            if (CatalogSymbols.DerivesFrom(type, types.ViewModelBase))
            {
                viewModels.Add(new ViewModelModel(name, CatalogSymbols.BuildExpression(type)));
            }

            if (CatalogSymbols.DerivesFrom(type, types.Control) &&
                CatalogSymbols.HasPublicParameterlessConstructor(type))
            {
                views.Add(new ViewEntryModel(type.Name, name));
            }

            foreach (ITypeSymbol destination in CatalogSymbols.NavigationDestinations(type, types.NavigationRequest))
            {
                navigationRequests.Add(new NavigationModel(name, CatalogSymbols.FullName(destination)));
            }
        }

        CatalogModel model = new(
            compilation.AssemblyName ?? "Unknown",
            CatalogSymbols.Identifier(compilation.AssemblyName ?? "Unknown"),
            Sorted(features, feature => feature.TypeName),
            Sorted(viewModels, viewModel => viewModel.TypeName),
            Sorted(views, view => view.TypeName),
            Sorted(navigationRequests, request => request.RequestTypeName + request.DestinationTypeName),
            ReferencedCatalogs(compilation));

        // An assembly that contributes nothing needs no catalog, and an empty one registered from a
        // module initializer is a type loaded for no reason.
        return model.IsEmpty ? null : model;
    }

    /// <summary>Stable order, so an unrelated edit does not rewrite the generated file.</summary>
    private static List<T> Sorted<T>(List<T> items, System.Func<T, string> key) =>
        items.OrderBy(key, System.StringComparer.Ordinal).ToList();

    /// <summary>
    /// The catalogs in referenced assemblies, so this assembly's initializer can touch each one.
    /// </summary>
    private static List<string> ReferencedCatalogs(Compilation compilation)
    {
        List<string> found = new();

        foreach (IAssemblySymbol reference in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            string candidate =
                $"{CatalogTypes.CatalogNamespace}.{CatalogSymbols.Identifier(reference.Name)}.{CatalogTypes.CatalogTypeName}";

            if (reference.GetTypeByMetadataName(candidate) is not null) found.Add("global::" + candidate);
        }

        found.Sort(System.StringComparer.Ordinal);
        return found;
    }
}

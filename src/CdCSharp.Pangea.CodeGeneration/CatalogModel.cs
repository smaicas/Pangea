using System.Collections.Generic;

namespace CdCSharp.Pangea.CodeGeneration;

/// <summary>A feature the assembly declares, and how to build it.</summary>
internal sealed class FeatureModel
{
    internal FeatureModel(string typeName) => TypeName = typeName;

    /// <summary>Fully qualified, so nothing depends on what the generated file happens to import.</summary>
    internal string TypeName { get; }
}

/// <summary>A view model, and the expression that builds one from a service provider.</summary>
internal sealed class ViewModelModel
{
    internal ViewModelModel(string typeName, string factory)
    {
        TypeName = typeName;
        Factory = factory;
    }

    internal string TypeName { get; }

    /// <summary>The body of a <c>Func&lt;IServiceProvider, object&gt;</c>, with <c>sp</c> in scope.</summary>
    internal string Factory { get; }
}

/// <summary>A control that can display a view model, and the name the convention matches it by.</summary>
internal sealed class ViewEntryModel
{
    internal ViewEntryModel(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }

    internal string Name { get; }

    internal string TypeName { get; }
}

/// <summary>A navigation request and the screen it names as its destination.</summary>
internal sealed class NavigationModel
{
    internal NavigationModel(string requestTypeName, string destinationTypeName)
    {
        RequestTypeName = requestTypeName;
        DestinationTypeName = destinationTypeName;
    }

    internal string RequestTypeName { get; }

    internal string DestinationTypeName { get; }
}

/// <summary>Everything one assembly contributes, ready to be written out.</summary>
internal sealed class CatalogModel
{
    internal CatalogModel(
        string assemblyName,
        string identifier,
        List<FeatureModel> features,
        List<ViewModelModel> viewModels,
        List<ViewEntryModel> views,
        List<NavigationModel> navigationRequests,
        List<string> referencedCatalogs)
    {
        AssemblyName = assemblyName;
        Identifier = identifier;
        Features = features;
        ViewModels = viewModels;
        Views = views;
        NavigationRequests = navigationRequests;
        ReferencedCatalogs = referencedCatalogs;
    }

    internal string AssemblyName { get; }

    /// <summary>The assembly name as a C# identifier, which is what namespaces the catalog.</summary>
    internal string Identifier { get; }

    internal List<FeatureModel> Features { get; }

    internal List<ViewModelModel> ViewModels { get; }

    internal List<ViewEntryModel> Views { get; }

    internal List<NavigationModel> NavigationRequests { get; }

    /// <summary>
    /// Fully qualified names of the catalogs in referenced assemblies.
    /// </summary>
    /// <remarks>
    /// A module initializer runs when its assembly is first touched, and a library of view models
    /// might not be touched until after startup has already asked what is in it. Naming them here
    /// makes this assembly's initializer touch each one, so every catalog is registered before the
    /// first question is asked.
    /// </remarks>
    internal List<string> ReferencedCatalogs { get; }

    internal bool IsEmpty =>
        Features.Count == 0 &&
        ViewModels.Count == 0 &&
        Views.Count == 0 &&
        NavigationRequests.Count == 0 &&
        ReferencedCatalogs.Count == 0;
}

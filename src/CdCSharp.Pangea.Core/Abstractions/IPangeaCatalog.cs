namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>How to build a view model, without the container having to work it out.</summary>
/// <param name="ViewModelType">The type the container registers.</param>
/// <param name="Create">Builds one, resolving whatever its constructor asks for.</param>
public sealed record PangeaViewModelEntry(Type ViewModelType, Func<IServiceProvider, object> Create);

/// <summary>A view, and the name a view model is matched to it by.</summary>
/// <param name="Name">The type's simple name, which is what the naming convention compares.</param>
/// <param name="ViewType">The control type.</param>
/// <param name="Create">Builds one with its parameterless constructor.</param>
public sealed record PangeaViewEntry(string Name, Type ViewType, Func<object> Create);

/// <summary>A navigation request and the screen it declares as its destination.</summary>
public sealed record PangeaNavigationEntry(Type RequestType, Type DestinationType);

/// <summary>
/// What one assembly contributes to startup, worked out while it was compiled.
/// </summary>
/// <remarks>
/// Everything here is otherwise found by scanning assemblies and reading their types: which classes
/// are features, which are view models, which control displays which screen. That scan is the
/// single largest thing a Pangea application does before its window appears, and it is the reason
/// the toolkit cannot be trimmed or compiled ahead of time - a type nothing references by name is a
/// type the trimmer removes.
/// <para>
/// A catalog is generated, not written. The generator emits one per project and registers it from a
/// module initializer; <c>PangeaCatalogs</c> is where they collect.
/// </para>
/// </remarks>
public interface IPangeaCatalog
{
    /// <summary>The assembly this describes, for diagnostics.</summary>
    string AssemblyName { get; }

    /// <summary>Builds each <see cref="IPangeaFeature"/> the assembly declares.</summary>
    IReadOnlyList<Func<IPangeaFeature>> Features { get; }

    /// <summary>Every view model the container should register, with how to build it.</summary>
    IReadOnlyList<PangeaViewModelEntry> ViewModels { get; }

    /// <summary>Every control that can display a view model, by name.</summary>
    IReadOnlyList<PangeaViewEntry> Views { get; }

    /// <summary>Every navigation request, and where it says it goes.</summary>
    IReadOnlyList<PangeaNavigationEntry> NavigationRequests { get; }
}

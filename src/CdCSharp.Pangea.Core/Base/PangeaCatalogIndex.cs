using CdCSharp.Pangea.Core.Abstractions;
using System.Reflection;

namespace CdCSharp.Pangea.Core.Base;

/// <summary>
/// Every generated catalog, merged into the questions startup actually asks.
/// </summary>
/// <remarks>
/// <para>
/// One instance per application, built during startup and registered in the container beside
/// <see cref="TypeRegistry"/>. Where the registry answers by having read every type in every
/// assembly, this answers from what the compiler already worked out.
/// </para>
/// <para>
/// It can be empty - a project the generator never ran in, a test that builds a container by hand -
/// and everything that reads it falls back to the registry when it is. The two must agree on what
/// they would answer, which is what makes the fallback safe rather than merely available.
/// </para>
/// </remarks>
public sealed class PangeaCatalogIndex
{
    /// <summary>Nothing generated: every caller falls back to scanning.</summary>
    public static PangeaCatalogIndex Empty { get; } = new([]);

    private readonly Dictionary<string, List<PangeaViewEntry>> _viewsByName = new(StringComparer.Ordinal);

    public PangeaCatalogIndex(IReadOnlyList<IPangeaCatalog> catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);

        List<Func<IPangeaFeature>> features = [];
        List<PangeaViewModelEntry> viewModels = [];
        List<PangeaNavigationEntry> navigationRequests = [];

        foreach (IPangeaCatalog catalog in catalogs)
        {
            features.AddRange(catalog.Features);
            viewModels.AddRange(catalog.ViewModels);
            navigationRequests.AddRange(catalog.NavigationRequests);

            foreach (PangeaViewEntry view in catalog.Views)
            {
                // All of them: a name is only unique within a namespace, and which one is meant
                // depends on who is asking.
                if (!_viewsByName.TryGetValue(view.Name, out List<PangeaViewEntry>? sameName))
                {
                    sameName = [];
                    _viewsByName[view.Name] = sameName;
                }

                sameName.Add(view);
            }
        }

        Catalogs = catalogs;
        Features = features;
        ViewModels = viewModels;
        NavigationRequests = navigationRequests;
    }

    /// <summary>The catalogs this was built from, by assembly.</summary>
    public IReadOnlyList<IPangeaCatalog> Catalogs { get; }

    public IReadOnlyList<Func<IPangeaFeature>> Features { get; }

    public IReadOnlyList<PangeaViewModelEntry> ViewModels { get; }

    public IReadOnlyList<PangeaNavigationEntry> NavigationRequests { get; }

    /// <summary>Whether anything was generated at all.</summary>
    public bool IsEmpty => Catalogs.Count == 0;

    /// <summary>
    /// Whether <paramref name="assembly"/> has a catalog of its own.
    /// </summary>
    /// <remarks>
    /// The question startup asks about the application's own assembly, and the reason it asks:
    /// the toolkit's assemblies always carry catalogs, so having some is no evidence that the
    /// application does. Trusting a catalog that does not cover the application would leave every
    /// view model it declares unregistered - which is worse than not having one at all.
    /// </remarks>
    public bool Covers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string? name = assembly.GetName().Name;

        return name is not null &&
               Catalogs.Any(catalog => string.Equals(catalog.AssemblyName, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The control of that name, or <see langword="null"/> if no catalog declares one.
    /// </summary>
    /// <param name="name">The control's simple name.</param>
    /// <param name="nearNamespace">
    /// The namespace of whatever is asking. When two assemblies - or two features of one - declare
    /// a control of the same name, the one nearest the caller is meant: <c>Orders.OrderView</c>
    /// belongs to <c>Orders.OrderViewModel</c>, not to <c>Billing.OrderView</c>.
    /// </param>
    public PangeaViewEntry? FindView(string name, string? nearNamespace = null)
    {
        if (!_viewsByName.TryGetValue(name, out List<PangeaViewEntry>? sameName)) return null;

        if (sameName.Count == 1 || nearNamespace is null) return sameName[0];

        return sameName
            .OrderByDescending(view => SharedNamespaceDepth(view.ViewType.Namespace, nearNamespace))
            .ThenBy(view => view.ViewType.FullName, StringComparer.Ordinal)
            .First();
    }

    /// <summary>How many leading namespace segments two namespaces have in common.</summary>
    private static int SharedNamespaceDepth(string? left, string? right)
    {
        if (left is null || right is null) return 0;

        string[] one = left.Split('.');
        string[] other = right.Split('.');

        int shared = 0;

        while (shared < one.Length && shared < other.Length &&
               string.Equals(one[shared], other[shared], StringComparison.Ordinal))
        {
            shared++;
        }

        return shared;
    }

    /// <summary>The view model of that simple name, or <see langword="null"/>.</summary>
    /// <remarks>
    /// By simple name because that is how the window manager looks for a main view model, and the
    /// only thing it has to go on is the convention.
    /// </remarks>
    public PangeaViewModelEntry? FindViewModel(string name) =>
        ViewModels.FirstOrDefault(entry => entry.ViewModelType.Name == name);
}

using Avalonia.Controls;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace CdCSharp.Pangea.Navigation;

/// <summary>
/// Resolves a view model to its view by name: <c>OrderViewModel</c> is displayed by <c>Order</c>.
/// </summary>
/// <remarks>
/// The same rule the window manager already uses to find a main window, applied to the shared type
/// registry so there is one type scan for the whole application. An explicit registration wins over
/// the convention, which is the escape hatch for views that do not follow it.
/// </remarks>
public class ViewLocator : IViewLocator
{
    private const string ViewModelSuffix = "ViewModel";
    private const string ModelSuffix = "Model";

    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegistry _typeRegistry;
    private readonly PangeaCatalogIndex _catalog;
    private readonly ConcurrentDictionary<Type, Type> _registrations = new();
    private readonly ConcurrentDictionary<Type, Resolution> _conventions = new();
    private readonly ConcurrentDictionary<Type, Func<object>> _factories = new();

    public ViewLocator(IServiceProvider serviceProvider, TypeRegistry typeRegistry, PangeaCatalogIndex? catalog = null)
    {
        _serviceProvider = serviceProvider;
        _typeRegistry = typeRegistry;
        _catalog = catalog ?? PangeaCatalogIndex.Empty;
    }

    public void Register<TViewModel, TView>()
        where TViewModel : class
        where TView : Control =>
        _registrations[typeof(TViewModel)] = typeof(TView);

    public bool CanLocate(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        return Find(viewModelType).View is not null;
    }

    public Control Locate(object viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        Type viewModelType = viewModel.GetType();
        Resolution resolution = Find(viewModelType);
        Type viewType = resolution.View ?? throw new InvalidOperationException(resolution.Failure);

        // From the container when it knows how to build it, so a view can take dependencies;
        // otherwise the parameterless constructor every XAML view has - written out by the
        // generator when there is a catalog, and reflected on when there is not.
        Control view = _serviceProvider.GetService(viewType) as Control
            ?? Build(viewType) as Control
            ?? throw new InvalidOperationException(
                $"'{viewType.FullName}' could not be created as a Control for '{viewModelType.Name}'.");

        view.DataContext = viewModel;
        return view;
    }

    /// <summary>
    /// Builds a view with the generated factory when the catalog has one, and by reflection when
    /// it does not.
    /// </summary>
    private object? Build(Type viewType) =>
        _factories.TryGetValue(viewType, out Func<object>? create)
            ? create()
            : Activator.CreateInstance(viewType);

    /// <summary>
    /// The registration when there is one, the convention otherwise - including its failure, which
    /// is remembered too.
    /// </summary>
    /// <remarks>
    /// Remembered because <see cref="CanLocate"/> is asked by the application-wide data template
    /// for every piece of content without a template of its own, every string on every button
    /// among them. Answering that from a dictionary is the difference between a lookup and a type
    /// scan per control.
    /// </remarks>
    private Resolution Find(Type viewModelType) =>
        _registrations.TryGetValue(viewModelType, out Type? registered)
            ? new Resolution(registered, null)
            : _conventions.GetOrAdd(viewModelType, ResolveByConvention);

    private Resolution ResolveByConvention(Type viewModelType)
    {
        string[] candidates = CandidateViewNames(viewModelType).ToArray();

        if (candidates.Length == 0)
        {
            return Resolution.Failed(
                $"'{viewModelType.Name}' does not end in '{ViewModelSuffix}', so no view name can be derived from it. " +
                "Register its view explicitly with IViewLocator.Register.");
        }

        // The catalog first: it answers without any assembly having been read.
        foreach (string candidate in candidates)
        {
            if (_catalog.FindView(candidate, viewModelType.Namespace) is not { } entry) continue;

            if (!typeof(Control).IsAssignableFrom(entry.ViewType))
            {
                return Resolution.Failed(
                    $"'{entry.ViewType.FullName}' was found for '{viewModelType.Name}' but is not a Control.");
            }

            _factories[entry.ViewType] = entry.Create;
            return new Resolution(entry.ViewType, null);
        }

        foreach (string candidate in candidates)
        {
            if (_typeRegistry.GetType(candidate) is not { } found) continue;

            if (!typeof(Control).IsAssignableFrom(found))
            {
                return Resolution.Failed(
                    $"'{found.FullName}' was found for '{viewModelType.Name}' but is not a Control.");
            }

            return new Resolution(found, null);
        }

        return Resolution.Failed(
            $"No view was found for '{viewModelType.Name}'. Name it {string.Join(" or ", candidates.Select(name => $"'{name}'"))}, " +
            "or register it explicitly with IViewLocator.Register.");
    }

    /// <summary>
    /// Both conventions in common use: <c>MainWindowViewModel</c> is displayed by
    /// <c>MainWindow</c>, and <c>OrderViewModel</c> by <c>OrderView</c>. Supporting one and not the
    /// other would make the rule a coin toss.
    /// </summary>
    private static IEnumerable<string> CandidateViewNames(Type viewModelType)
    {
        string name = viewModelType.Name;

        if (!name.EndsWith(ViewModelSuffix, StringComparison.Ordinal)) yield break;

        yield return name[..^ModelSuffix.Length];   // OrderViewModel -> OrderView
        yield return name[..^ViewModelSuffix.Length];  // MainWindowViewModel -> MainWindow
    }

    private sealed record Resolution(Type? View, string? Failure)
    {
        public static Resolution Failed(string failure) => new(null, failure);
    }
}

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
    private readonly ConcurrentDictionary<Type, Type> _registrations = new();

    public ViewLocator(IServiceProvider serviceProvider, TypeRegistry typeRegistry)
    {
        _serviceProvider = serviceProvider;
        _typeRegistry = typeRegistry;
    }

    public void Register<TViewModel, TView>()
        where TViewModel : class
        where TView : Control =>
        _registrations[typeof(TViewModel)] = typeof(TView);

    public Control Locate(object viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        Type viewModelType = viewModel.GetType();
        Type viewType = _registrations.GetOrAdd(viewModelType, ResolveByConvention);

        // From the container when it knows how to build it, so a view can take dependencies;
        // otherwise the parameterless constructor every XAML view has.
        Control view = _serviceProvider.GetService(viewType) as Control
            ?? Activator.CreateInstance(viewType) as Control
            ?? throw new InvalidOperationException(
                $"'{viewType.FullName}' could not be created as a Control for '{viewModelType.Name}'.");

        view.DataContext = viewModel;
        return view;
    }

    private Type ResolveByConvention(Type viewModelType)
    {
        string[] candidates = CandidateViewNames(viewModelType).ToArray();

        if (candidates.Length == 0)
        {
            throw new InvalidOperationException(
                $"'{viewModelType.Name}' does not end in '{ViewModelSuffix}', so no view name can be derived from it. " +
                "Register its view explicitly with IViewLocator.Register.");
        }

        foreach (string candidate in candidates)
        {
            if (_typeRegistry.GetType(candidate) is not { } found) continue;

            if (!typeof(Control).IsAssignableFrom(found))
            {
                throw new InvalidOperationException(
                    $"'{found.FullName}' was found for '{viewModelType.Name}' but is not a Control.");
            }

            return found;
        }

        throw new InvalidOperationException(
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
}

using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Navigation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CdCSharp.Pangea.Navigation;

/// <summary>
/// One navigation stack for the application.
/// </summary>
/// <remarks>
/// Every navigation runs on the UI thread: it ends in a property change a host is bound to, and
/// the arrival hooks are where a screen loads what it displays. Marshalling here means a view model
/// can navigate from a background thread without knowing it.
/// </remarks>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IUIDispatcher _dispatcher;
    private readonly Stack<object> _history = new();
    private readonly bool _discardsViewModels;

    /// <summary>
    /// The typed arrival hook is found by reflection, once per view model and request type pair.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type ViewModel, Type Request), MethodInfo?> TypedHooks = new();

    private object? _currentViewModel;

    /// <remarks>
    /// The options are read for one thing: whether view models are transient. A screen this service
    /// drops is a screen nothing else holds, so it is told to let go of what it subscribed to -
    /// which would be wrong for a view model the container hands out again.
    /// </remarks>
    public NavigationService(
        IServiceProvider serviceProvider, IUIDispatcher dispatcher, IOptions<PangeaOptions>? options = null)
    {
        _serviceProvider = serviceProvider;
        _dispatcher = dispatcher;
        _discardsViewModels = (options?.Value.DI.ViewModelLifetime ?? ServiceLifetime.Transient) ==
                              ServiceLifetime.Transient;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public object? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (ReferenceEquals(_currentViewModel, value)) return;

            _currentViewModel = value;
            OnPropertyChanged();
        }
    }

    public bool CanGoBack => _history.Count > 0;

    public Task<bool> NavigateToAsync<TViewModel>() where TViewModel : class =>
        _dispatcher.InvokeAsync(() => NavigateCore(typeof(TViewModel), request: null));

    public Task<bool> NavigateToAsync<TViewModel>(INavigationRequest<TViewModel> request)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(request);
        return _dispatcher.InvokeAsync(() => NavigateCore(typeof(TViewModel), request));
    }

    public Task<bool> GoBackAsync() => _dispatcher.InvokeAsync(GoBackCore);

    public void ClearHistory()
    {
        bool could = CanGoBack;

        // Everything on the stack is now unreachable: nothing can navigate back to it, and the only
        // thing keeping it alive would be whatever it subscribed to on the way in.
        foreach (object forgotten in _history) Discard(forgotten);

        _history.Clear();

        if (could) OnPropertyChanged(nameof(CanGoBack));
    }

    private async Task<bool> NavigateCore(Type viewModelType, object? request)
    {
        if (!await LeaveCurrentAsync()) return false;

        if (_currentViewModel is not null)
        {
            _history.Push(_currentViewModel);
        }

        object target = _serviceProvider.GetRequiredService(viewModelType);

        CurrentViewModel = target;
        OnPropertyChanged(nameof(CanGoBack));

        await ArriveAsync(target, request);
        return true;
    }

    private async Task<bool> GoBackCore()
    {
        if (!CanGoBack) return false;
        if (!await LeaveCurrentAsync()) return false;

        object previous = _history.Pop();
        object? left = _currentViewModel;

        CurrentViewModel = previous;
        OnPropertyChanged(nameof(CanGoBack));

        // Going forward puts the screen on the history stack, so it is coming back. Going back does
        // not: this is the one place a view model is dropped for good.
        Discard(left);

        // Going back is a return, not an arrival with a request: the screen is the one that was
        // already built, and re-running its request hook would reload it against stale data.
        await ArriveAsync(previous, request: null);
        return true;
    }

    /// <summary>
    /// Tells a view model nothing will use it again to release what it subscribed to.
    /// </summary>
    /// <remarks>
    /// Not disposal: the container tracks every transient <see cref="IDisposable"/> it creates and
    /// holds it until the process ends, so a disposable view model would leak harder than the
    /// subscription this is here to release.
    /// </remarks>
    private void Discard(object? viewModel)
    {
        if (!_discardsViewModels) return;

        (viewModel as IDiscardable)?.Discard();
    }

    /// <summary>Asks the current view model to leave, and tells it that it did.</summary>
    private async Task<bool> LeaveCurrentAsync()
    {
        if (_currentViewModel is not INavigationAware leaving) return true;

        if (!await leaving.CanNavigateAwayAsync()) return false;

        await leaving.OnNavigatedFromAsync();
        return true;
    }

    /// <summary>
    /// Runs the arrival hook: the typed one when a request was supplied and the view model accepts
    /// it, the parameterless one otherwise.
    /// </summary>
    private static async Task ArriveAsync(object target, object? request)
    {
        if (request is not null)
        {
            MethodInfo? hook = TypedHooks.GetOrAdd(
                (target.GetType(), request.GetType()),
                key => FindTypedHook(key.ViewModel, key.Request));

            if (hook is not null)
            {
                await (Task)hook.Invoke(target, [request])!;
                return;
            }
        }

        if (target is INavigationAware aware)
        {
            await aware.OnNavigatedToAsync();
        }
    }

    private static MethodInfo? FindTypedHook(Type viewModelType, Type requestType) =>
        viewModelType.GetInterfaces()
            .Where(candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(INavigationAware<>)
                && candidate.GetGenericArguments()[0].IsAssignableFrom(requestType))
            .Select(candidate => viewModelType.GetInterfaceMap(candidate).TargetMethods[0])
            .FirstOrDefault();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

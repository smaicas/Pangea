using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Navigation.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CdCSharp.Pangea.Testing.Fakes;

/// <summary>Where a navigation was headed, and what it was carrying.</summary>
public sealed record NavigationAttempt(Type Destination, object? Request);

/// <summary>
/// Records navigations instead of performing them.
/// </summary>
/// <remarks>
/// The real service builds the destination from the container and runs its arrival hooks, which
/// makes "did this button navigate to the right screen" a test of two view models at once. This
/// answers the question about one: what was asked for, and with what.
/// <para>
/// Nothing is shown, so <see cref="CurrentViewModel"/> stays where a test puts it. Set
/// <see cref="Refuse"/> to have every navigation report that it was cancelled, as the real service
/// does when the current screen refuses to be left.
/// </para>
/// </remarks>
public sealed class RecordingNavigationService : INavigationService
{
    private readonly List<object> _history = [];

    private object? _currentViewModel;

    /// <summary>Every navigation asked for, in order.</summary>
    public List<NavigationAttempt> Navigations { get; } = [];

    /// <summary>How many times going back was asked for.</summary>
    public int GoBackCount { get; private set; }

    /// <summary>Whether every navigation reports that it was refused.</summary>
    public bool Refuse { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The screen a test says is showing. Settable, because nothing here builds one.</summary>
    public object? CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
            if (ReferenceEquals(_currentViewModel, value)) return;

            if (_currentViewModel is not null) _history.Add(_currentViewModel);

            _currentViewModel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    public bool CanGoBack => _history.Count > 0;

    /// <summary>The destination of the last navigation, or <see langword="null"/> if there was none.</summary>
    public Type? LastDestination => Navigations.Count > 0 ? Navigations[^1].Destination : null;

    /// <summary>The request carried by the last navigation, typed.</summary>
    public TRequest? LastRequest<TRequest>() where TRequest : class =>
        Navigations.Count > 0 ? Navigations[^1].Request as TRequest : null;

    public Task<bool> NavigateToAsync<TViewModel>() where TViewModel : class =>
        Record(typeof(TViewModel), request: null);

    public Task<bool> NavigateToAsync<TViewModel>(INavigationRequest<TViewModel> request)
        where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(request);
        return Record(typeof(TViewModel), request);
    }

    public Task<bool> GoBackAsync()
    {
        GoBackCount++;

        if (Refuse || !CanGoBack) return Task.FromResult(false);

        object previous = _history[^1];
        _history.RemoveAt(_history.Count - 1);

        _currentViewModel = previous;
        OnPropertyChanged(nameof(CurrentViewModel));
        OnPropertyChanged(nameof(CanGoBack));

        return Task.FromResult(true);
    }

    public void ClearHistory()
    {
        bool could = CanGoBack;
        _history.Clear();

        if (could) OnPropertyChanged(nameof(CanGoBack));
    }

    private Task<bool> Record(Type destination, object? request)
    {
        Navigations.Add(new NavigationAttempt(destination, request));

        return Task.FromResult(!Refuse);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

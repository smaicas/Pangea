using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CdCSharp.Pangea.Core.Base;

public abstract class ViewModelBase : INotifyPropertyChanged, INotifyDataErrorInfo, INavigationAware, IDiscardable
{
    protected readonly IRelayCommandFactory CommandFactory;

    private readonly ConcurrentDictionary<(string Owner, MethodInfo Execute, MethodInfo? CanExecute), object> _commands = new();
    private readonly IUIDispatcher? _dispatcher;
    private readonly List<Action> _releases = [];
    private readonly object _releaseGate = new();

    private int _running;
    private bool _discarded;

    protected ViewModelBase(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        CommandFactory = serviceProvider.GetRequiredService<IRelayCommandFactory>();

        // Both optional: a view model built by hand in a test has neither, and everything that uses
        // them is a convenience rather than a precondition.
        _dispatcher = serviceProvider.GetService<IUIDispatcher>();
        Logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(GetType());
    }

    /// <summary>
    /// The log this view model writes to, or <see langword="null"/> when the application configured
    /// no logging.
    /// </summary>
    protected ILogger? Logger { get; }

    /// <summary>
    /// True while any command this view model created is running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A command already refuses to run twice at once and reports <c>CanExecute == false</c> while
    /// it does, so the button that started the work needs nothing from this. What needs it is
    /// everything else on the screen: the spinner, the other buttons, the "saving..." line.
    /// </para>
    /// <para>
    /// Counts the commands built by <see cref="CreateCommand(Action, Func{bool}?, string?)"/> and
    /// its overloads. A command constructed by hand is not part of it.
    /// </para>
    /// </remarks>
    public bool IsBusy => Volatile.Read(ref _running) > 0;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
    public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;
    public virtual Task<bool> CanNavigateAwayAsync() => Task.FromResult(true);

    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _errors = new();

    /// <summary>Raised when the errors for a property change, as Avalonia's bindings expect.</summary>
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>Whether any property currently fails validation.</summary>
    public bool HasErrors => !_errors.IsEmpty;

    /// <summary>
    /// The validation messages for a property, or for the whole view model when
    /// <paramref name="propertyName"/> is null or empty.
    /// </summary>
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return _errors.Values.SelectMany(messages => messages).ToList();
        }

        return _errors.TryGetValue(propertyName!, out IReadOnlyList<string>? found)
            ? found
            : Array.Empty<string>();
    }

    /// <summary>
    /// Validates one property against the validation attributes declared on it.
    /// </summary>
    /// <remarks>
    /// Called by the generated setters. The rules are read from the property rather than emitted
    /// into it, so an application's own <see cref="ValidationAttribute"/> works with no support
    /// from the generator at all.
    /// </remarks>
    protected void ValidateProperty(object? value, [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        List<ValidationResult> results = [];

        Validator.TryValidateProperty(
            value,
            new ValidationContext(this) { MemberName = propertyName },
            results);

        SetErrors(propertyName!, results
            .Select(result => result.ErrorMessage ?? "Invalid value.")
            .ToList());
    }

    /// <summary>
    /// Validates every property that declares validation attributes, and reports whether the view
    /// model is now valid. What a Save button asks before doing anything.
    /// </summary>
    public bool ValidateAll()
    {
        foreach (PropertyInfo property in GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.CanRead &&
                                        property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any()))
        {
            ValidateProperty(property.GetValue(this), property.Name);
        }

        return !HasErrors;
    }

    private void SetErrors(string propertyName, IReadOnlyList<string> messages)
    {
        bool had = _errors.ContainsKey(propertyName);

        if (messages.Count == 0)
        {
            if (!had) return;

            _errors.TryRemove(propertyName, out _);
        }
        else
        {
            if (had && _errors[propertyName].SequenceEqual(messages)) return;

            _errors[propertyName] = messages;
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected RelayCommand CreateCommand(
        Action execute, Func<bool>? canExecute = null, [CallerMemberName] string? owner = null) =>
        Cached(owner, execute.Method, canExecute?.Method,
            () => CommandFactory.Create(execute, canExecute, OnCommandError));

    protected RelayCommand CreateCommand(
        Func<Task> executeAsync, Func<bool>? canExecute = null, [CallerMemberName] string? owner = null) =>
        Cached(owner, executeAsync.Method, canExecute?.Method,
            () => CommandFactory.Create(executeAsync, canExecute, OnCommandError));

    protected RelayCommand<T> CreateCommand<T>(
        Action<T?> execute, Func<T?, bool>? canExecute = null, [CallerMemberName] string? owner = null) =>
        Cached(owner, execute.Method, canExecute?.Method,
            () => CommandFactory.Create(execute, canExecute, OnCommandError));

    protected RelayCommand<T> CreateCommand<T>(
        Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null, [CallerMemberName] string? owner = null) =>
        Cached(owner, executeAsync.Method, canExecute?.Method,
            () => CommandFactory.Create(executeAsync, canExecute, OnCommandError));

    /// <summary>
    /// One command per declaration, however many times it is read.
    /// </summary>
    /// <remarks>
    /// Commands are written as expression-bodied properties, so without this every read built a new
    /// command: the binding held one instance and <c>RaiseCanExecuteChanged</c> - the call the
    /// generator emits - reached another. The button stayed at whatever CanExecute said when it was
    /// bound.
    /// <para>
    /// A constructor is left alone. It assigns into a field, which already gives one stable
    /// instance, and every command it builds shares the same member name - so caching there buys
    /// nothing and would merge two commands that happen to share a body.
    /// </para>
    /// </remarks>
    private TCommand Cached<TCommand>(
        string? owner, MethodInfo execute, MethodInfo? canExecute, Func<TCommand> build)
        where TCommand : class
    {
        if (string.IsNullOrEmpty(owner) || owner is ".ctor" or ".cctor")
        {
            return Watch(build());
        }

        return (TCommand)_commands.GetOrAdd((owner, execute, canExecute), _ => Watch(build()));
    }

    /// <summary>
    /// Keeps <see cref="IsBusy"/> in step with the commands, and hands back what was built.
    /// </summary>
    private TCommand Watch<TCommand>(TCommand command) where TCommand : class
    {
        if (command is RelayCommandBase watchable) watchable.PropertyChanged += OnCommandPropertyChanged;

        return command;
    }

    private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RelayCommandBase.IsExecuting)) return;
        if (sender is not RelayCommandBase command) return;

        int running = command.IsExecuting
            ? Interlocked.Increment(ref _running)
            : Interlocked.Decrement(ref _running);

        // Only the transitions that change the answer: the first command to start and the last to
        // finish. Anything in between leaves IsBusy where it was.
        if (running is not (0 or 1)) return;

        NotifyOnUIThread(nameof(IsBusy));

        // Every command, not just the one that changed: a screen whose Delete is gated on "not busy"
        // has no other way of hearing that Save has started. The command that is running already
        // refuses to run again on its own.
        RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Re-evaluates <c>CanExecute</c> on every command this view model created.
    /// </summary>
    /// <remarks>
    /// The generator raises this for the properties a predicate reads, so a view model rarely calls
    /// it. What it does not know about is state living somewhere else - a service's event, a timer -
    /// and this is the answer for that.
    /// </remarks>
    protected void RaiseCanExecuteChanged()
    {
        foreach (object command in _commands.Values)
        {
            (command as RelayCommandBase)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Raises a property change on the UI thread.
    /// </summary>
    /// <remarks>
    /// A command body finishes on whatever thread it was left on, and Avalonia rejects a binding
    /// notified from anywhere but the UI thread.
    /// </remarks>
    private void NotifyOnUIThread(string propertyName)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            OnPropertyChanged(propertyName);
            return;
        }

        _dispatcher.Post(() => OnPropertyChanged(propertyName));
    }

    /// <summary>
    /// Subscribes to an event and releases it when this view model is discarded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leak this exists for: a screen subscribes to a service that outlives it, is navigated
    /// away from, and is never collected - the service's event list is holding it. Nothing about
    /// that is visible until the application has been used for a while.
    /// </para>
    /// <code>
    /// Subscribe(handler => _repository.Changed += handler, handler => _repository.Changed -= handler, OnChanged);
    /// </code>
    /// </remarks>
    /// <returns>A handle that releases the subscription early. Ignoring it is the normal case.</returns>
    protected IDisposable Subscribe(
        Action<EventHandler> subscribe, Action<EventHandler> unsubscribe, EventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(unsubscribe);
        ArgumentNullException.ThrowIfNull(handler);

        return Register(() => subscribe(handler), () => unsubscribe(handler));
    }

    /// <inheritdoc cref="Subscribe(Action{EventHandler}, Action{EventHandler}, EventHandler)"/>
    protected IDisposable Subscribe<TArgs>(
        Action<EventHandler<TArgs>> subscribe, Action<EventHandler<TArgs>> unsubscribe, EventHandler<TArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(unsubscribe);
        ArgumentNullException.ThrowIfNull(handler);

        return Register(() => subscribe(handler), () => unsubscribe(handler));
    }

    /// <summary>Subscribes to property changes on something else, released on discard.</summary>
    protected IDisposable Subscribe(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);

        return Register(() => source.PropertyChanged += handler, () => source.PropertyChanged -= handler);
    }

    /// <summary>Subscribes to a collection someone else owns, released on discard.</summary>
    protected IDisposable Subscribe(INotifyCollectionChanged source, NotifyCollectionChangedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(handler);

        return Register(() => source.CollectionChanged += handler, () => source.CollectionChanged -= handler);
    }

    /// <summary>
    /// Hands something that has to be released to this view model's discard, for the subscriptions
    /// this class has no overload for.
    /// </summary>
    protected IDisposable Track(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return Register(subscribe: null, subscription.Dispose);
    }

    private IDisposable Register(Action? subscribe, Action release)
    {
        lock (_releaseGate)
        {
            // Already discarded: subscribing now would be a subscription nothing will ever release,
            // which is the failure this method exists to prevent.
            if (_discarded) return Subscription.Released;

            subscribe?.Invoke();
            _releases.Add(release);
        }

        return new Subscription(this, release);
    }

    /// <summary>
    /// Releases everything this view model subscribed to.
    /// </summary>
    /// <remarks>
    /// Called by the navigation service for a screen it drops. A view model held anywhere else is
    /// discarded by whoever holds it. Running it twice does nothing the second time.
    /// </remarks>
    public void Discard()
    {
        List<Action> releases;

        lock (_releaseGate)
        {
            if (_discarded) return;

            _discarded = true;
            releases = [.. _releases];
            _releases.Clear();
        }

        foreach (Action release in releases)
        {
            try
            {
                release();
            }
            catch (Exception ex)
            {
                // One subscription that will not let go must not strand the rest of them.
                Logger?.LogError(ex, "A subscription of {ViewModel} could not be released", GetType().Name);
            }
        }

        foreach (object command in _commands.Values)
        {
            if (command is RelayCommandBase watched) watched.PropertyChanged -= OnCommandPropertyChanged;
        }

        OnDiscarded();
    }

    /// <summary>Anything else this view model has to let go of. Called at the end of <see cref="Discard"/>.</summary>
    protected virtual void OnDiscarded() { }

    private void Release(Action release)
    {
        lock (_releaseGate)
        {
            if (!_releases.Remove(release)) return;
        }

        release();
    }

    /// <summary>A single subscription, releasable on its own before the view model is discarded.</summary>
    private sealed class Subscription : IDisposable
    {
        /// <summary>What a subscription registered after the discard hands back: nothing to release.</summary>
        public static readonly IDisposable Released = new Subscription(owner: null, release: null);

        private readonly ViewModelBase? _owner;
        private Action? _release;

        public Subscription(ViewModelBase? owner, Action? release)
        {
            _owner = owner;
            _release = release;
        }

        public void Dispose()
        {
            Action? release = Interlocked.Exchange(ref _release, null);

            if (release is null || _owner is null) return;

            _owner.Release(release);
        }
    }

    /// <summary>
    /// What a command does with a failure nobody else caught.
    /// </summary>
    /// <remarks>
    /// Logged, because the alternative is what this used to do: nothing. A command whose body threw
    /// leaves a button that visibly does nothing, with no message, no log line and no crash - the
    /// most expensive kind of bug to find, and one nobody chooses on purpose. Override to add to it;
    /// call the base to keep the log line.
    /// </remarks>
    protected virtual void OnCommandError(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (Logger is not null)
        {
            Logger.LogError(ex, "A command on {ViewModel} failed", GetType().Name);
            return;
        }

        // No logging configured. The debugger is the only place left where this can still be seen,
        // and silence is not an option worth defaulting to.
        Debug.WriteLine($"[Pangea] A command on {GetType().Name} failed: {ex}");
    }
}
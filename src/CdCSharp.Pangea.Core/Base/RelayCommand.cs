using CdCSharp.Pangea.Core.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CdCSharp.Pangea.Core.Base;

/// <summary>
/// Execution, error routing and CanExecute notification shared by <see cref="RelayCommand"/> and
/// <see cref="RelayCommand{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Synchronous command bodies run on the UI thread. A command is a UI concept: its body almost
/// always touches the view model that the UI is bound to, and Avalonia rejects cross-thread access.
/// Callers that need background work should use the <see cref="Func{Task}"/> overloads and move off
/// the UI thread themselves, where the intent is explicit.
/// </para>
/// <para>
/// Failures are always reported to the error handler supplied at construction. Whether they also
/// surface to the caller depends on whether the caller can do anything about them:
/// <see cref="ExecuteAsync"/> rethrows, <see cref="Execute"/> cannot and does not.
/// </para>
/// </remarks>
public abstract class RelayCommandBase : ICommand, INotifyPropertyChanged
{
    private readonly IUIDispatcher? _dispatcher;
    private readonly Action<Exception>? _onError;
    private volatile bool _isExecuting;
    private volatile bool _canExecuteChangedScheduled;

    protected RelayCommandBase(IUIDispatcher? dispatcher, Action<Exception>? onError)
    {
        _dispatcher = dispatcher;
        _onError = onError;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CanExecuteChanged;

    /// <summary>True while the command body is running. Blocks re-entrant execution.</summary>
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting == value) return;

            _isExecuting = value;
            OnPropertyChanged();
            RaiseCanExecuteChanged();
        }
    }

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting) return false;

        try
        {
            return CanExecuteCore(parameter);
        }
        catch (Exception ex)
        {
            // A predicate that throws must not take down the binding that is evaluating it.
            ReportError(ex);
            return false;
        }
    }

    /// <summary>
    /// ICommand entry point, invoked by bindings. Nothing can observe an exception escaping an
    /// async void method except the process-wide unhandled handler, so failures stop here.
    /// </summary>
    public async void Execute(object? parameter = null)
    {
        try
        {
            await RunAsync(parameter);
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    /// <summary>
    /// Awaitable entry point. Reports the failure and rethrows, so the caller can react.
    /// </summary>
    public async Task ExecuteAsync(object? parameter = null)
    {
        try
        {
            await RunAsync(parameter);
        }
        catch (Exception ex)
        {
            ReportError(ex);
            throw;
        }
    }

    /// <summary>
    /// Re-evaluates CanExecute. Raised inline on the UI thread; from any other thread it is
    /// marshalled and coalesced, so a burst of property changes produces a single notification.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_canExecuteChangedScheduled) return;
        _canExecuteChangedScheduled = true;

        _dispatcher.Post(() =>
        {
            _canExecuteChangedScheduled = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    protected abstract bool CanExecuteCore(object? parameter);

    protected abstract Task ExecuteCoreAsync(object? parameter);

    /// <summary>Runs a synchronous command body on the UI thread.</summary>
    protected Task RunOnUIThread(Action body)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            body();
        }
        else
        {
            _dispatcher.Invoke(body);
        }

        return Task.CompletedTask;
    }

    protected void ReportError(Exception exception) => _onError?.Invoke(exception);

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async Task RunAsync(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await ExecuteCoreAsync(parameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }
}

/// <summary>Parameterless command.</summary>
public class RelayCommand : RelayCommandBase
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(
        Action execute,
        Func<bool>? canExecute = null,
        IUIDispatcher? dispatcher = null,
        Action<Exception>? onError = null)
        : base(dispatcher, onError)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = _ => RunOnUIThread(execute);
        _canExecute = canExecute;
    }

    public RelayCommand(
        Func<Task> executeAsync,
        Func<bool>? canExecute = null,
        IUIDispatcher? dispatcher = null,
        Action<Exception>? onError = null)
        : base(dispatcher, onError)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        _execute = _ => executeAsync();
        _canExecute = canExecute;
    }

    public RelayCommand(
        Func<object?, Task> executeAsync,
        Func<object?, bool>? canExecute = null,
        IUIDispatcher? dispatcher = null,
        Action<Exception>? onError = null)
        : base(dispatcher, onError)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        _execute = executeAsync;
        _canExecute = canExecute is null ? null : () => canExecute(null);
    }

    protected override bool CanExecuteCore(object? parameter) => _canExecute?.Invoke() ?? true;

    protected override Task ExecuteCoreAsync(object? parameter) => _execute(parameter);
}

/// <summary>Command taking a typed parameter, coerced from the binding's untyped value.</summary>
public class RelayCommand<T> : RelayCommandBase
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(
        Action<T?> execute,
        Func<T?, bool>? canExecute = null,
        IUIDispatcher? dispatcher = null,
        Action<Exception>? onError = null)
        : base(dispatcher, onError)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = parameter => RunOnUIThread(() => execute(parameter));
        _canExecute = canExecute;
    }

    public RelayCommand(
        Func<T?, Task> executeAsync,
        Func<T?, bool>? canExecute = null,
        IUIDispatcher? dispatcher = null,
        Action<Exception>? onError = null)
        : base(dispatcher, onError)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        _execute = executeAsync;
        _canExecute = canExecute;
    }

    public Task ExecuteAsync(T? parameter) => ExecuteAsync((object?)parameter);

    protected override bool CanExecuteCore(object? parameter) =>
        _canExecute?.Invoke(CastParameter(parameter)) ?? true;

    protected override Task ExecuteCoreAsync(object? parameter) => _execute(CastParameter(parameter));

    /// <summary>
    /// Coerces the binding's untyped parameter to <typeparamref name="T"/>. A value that cannot be
    /// converted yields default rather than throwing: bindings routinely pass null before their
    /// source is set, and a command should stay disabled rather than blow up during evaluation.
    /// </summary>
    private static T? CastParameter(object? parameter)
    {
        if (parameter is T typed) return typed;
        if (parameter is null) return default;

        try
        {
            Type target = typeof(T);
            if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                target = Nullable.GetUnderlyingType(target)!;
            }

            return (T?)Convert.ChangeType(parameter, target);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return default;
        }
    }
}

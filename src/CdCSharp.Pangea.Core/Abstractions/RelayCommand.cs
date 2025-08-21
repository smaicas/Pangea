using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CdCSharp.Pangea.Core.Base;

public class RelayCommand : ICommand, INotifyPropertyChanged
{
    private readonly Func<object?, bool>? _canExecute;
    private readonly Func<object?, Task>? _executeAsync;
    private volatile bool _isExecuting;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(execute != null ? _ => Task.Run(execute) : throw new ArgumentNullException(nameof(execute)),
            canExecute != null ? _ => canExecute() : null)
    {
    }

    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        : this(executeAsync != null ? _ => executeAsync() : throw new ArgumentNullException(nameof(executeAsync)),
            canExecute != null ? _ => canExecute() : null)
    {
    }

    public RelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }
    }

    public event EventHandler? CanExecuteChanged;

    public virtual bool CanExecute(object? parameter)
    {
        try
        {
            return !IsExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }
        catch
        {
            return false;
        }
    }

    public async void Execute(object? parameter = null)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await _executeAsync!(parameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task ExecuteAsync(object? parameter = null)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await _executeAsync!(parameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public void RaiseCanExecuteChanged()
    {
        try
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Ignore notification errors
        }
    }

    public void RaiseCanExecuteChangedSafe()
    {
        try
        {
            if (SynchronizationContext.Current != null)
                SynchronizationContext.Current.Post(_ => RaiseCanExecuteChanged(), null);
            else
                Task.Run(RaiseCanExecuteChanged);
        }
        catch
        {
            // Ignore notification errors
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        try
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        catch
        {
            // Ignore notification errors
        }
    }
}

public class RelayCommand<T> : ICommand, INotifyPropertyChanged
{
    private readonly Func<T?, bool>? _canExecute;
    private readonly Func<T?, Task>? _executeAsync;
    private volatile bool _isExecuting;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        : this(
            execute != null
                ? param => Task.Run(() => execute(param))
                : throw new ArgumentNullException(nameof(execute)), canExecute)
    {
    }

    public RelayCommand(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }
    }

    public event EventHandler? CanExecuteChanged;

    public virtual bool CanExecute(object? parameter)
    {
        try
        {
            return !IsExecuting && (_canExecute?.Invoke(CastParameter(parameter)) ?? true);
        }
        catch
        {
            return false;
        }
    }

    public async void Execute(object? parameter)
    {
        T? typedParameter = CastParameter(parameter);
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await _executeAsync!(typedParameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await _executeAsync!(parameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public async Task ExecuteAsync(object? parameter = null) => await ExecuteAsync(CastParameter(parameter));

    public void RaiseCanExecuteChanged()
    {
        try
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Ignore notification errors
        }
    }

    public void RaiseCanExecuteChangedSafe()
    {
        try
        {
            if (SynchronizationContext.Current != null)
                SynchronizationContext.Current.Post(_ => RaiseCanExecuteChanged(), null);
            else
                Task.Run(RaiseCanExecuteChanged);
        }
        catch
        {
            // Ignore notification errors
        }
    }

    private T? CastParameter(object? parameter)
    {
        try
        {
            if (parameter == null && !typeof(T).IsValueType)
                return default;

            if (parameter is T typedParameter)
                return typedParameter;

            if (parameter != null)
            {
                Type targetType = typeof(T);
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    targetType = Nullable.GetUnderlyingType(targetType)!;

                return (T?)Convert.ChangeType(parameter, targetType);
            }

            return default;
        }
        catch
        {
            return default;
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        try
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        catch
        {
            // Ignore notification errors
        }
    }
}
using CdCSharp.Pangea.Core.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CdCSharp.Pangea.Core.Base;

public class RelayCommand : ICommand, INotifyPropertyChanged
{
    private readonly Func<object?, bool>? _canExecute;
    private readonly Func<object?, Task>? _executeAsync;
    private readonly IUIDispatcher? _dispatcher;
    private volatile bool _isExecuting;
    private volatile bool _updateScheduled;

    public RelayCommand(Action execute, Func<bool>? canExecute = null, IUIDispatcher? dispatcher = null)
        : this(execute != null ? _ => Task.Run(execute) : throw new ArgumentNullException(nameof(execute)),
            canExecute != null ? _ => canExecute() : null, dispatcher)
    {
    }

    public RelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, IUIDispatcher? dispatcher = null)
        : this(executeAsync != null ? _ => executeAsync() : throw new ArgumentNullException(nameof(executeAsync)),
            canExecute != null ? _ => canExecute() : null, dispatcher)
    {
    }

    public RelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null,
        IUIDispatcher? dispatcher = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _dispatcher = dispatcher;
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

    private EventHandler? _canExecuteChanged;

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            System.Diagnostics.Debug.WriteLine(
                $"[RelayCommand] CanExecuteChanged SUSCRITO por: {value?.Target?.GetType().Name}");
            _canExecuteChanged += value;
        }
        remove
        {
            System.Diagnostics.Debug.WriteLine(
                $"[RelayCommand] CanExecuteChanged DESUSCRITO por: {value?.Target?.GetType().Name}");
            _canExecuteChanged -= value;
        }
    }

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
        if (_updateScheduled) return;

        _updateScheduled = true;

        System.Diagnostics.Debug.WriteLine(
            $"[RelayCommand] RaiseCanExecuteChanged iniciado. Suscriptores: {_canExecuteChanged?.GetInvocationList()?.Length ?? 0}");

        if (_dispatcher != null)
        {
            _dispatcher.Post(() =>
            {
                _updateScheduled = false;
                try
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[RelayCommand] Ejecutando CanExecuteChanged en UI Thread. Suscriptores: {_canExecuteChanged?.GetInvocationList()?.Length ?? 0}");
                    _canExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RelayCommand] ERROR en CanExecuteChanged: {ex.Message}");
                }
            });
        }
        else
        {
            _updateScheduled = false;
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RelayCommand] Ejecutando CanExecuteChanged SIN dispatcher. Suscriptores: {_canExecuteChanged?.GetInvocationList()?.Length ?? 0}");
                _canExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RelayCommand] ERROR en CanExecuteChanged: {ex.Message}");
            }
        }
    }

    public void RaiseCanExecuteChangedSafe()
    {
        // Método legacy mantenido para compatibilidad
        RaiseCanExecuteChanged();
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
    private readonly IUIDispatcher? _dispatcher;
    private volatile bool _isExecuting;
    private volatile bool _updateScheduled;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null, IUIDispatcher? dispatcher = null)
        : this(
            execute != null
                ? param => Task.Run(() => execute(param))
                : throw new ArgumentNullException(nameof(execute)), canExecute, dispatcher)
    {
    }

    public RelayCommand(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null,
        IUIDispatcher? dispatcher = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _dispatcher = dispatcher;
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
        if (_updateScheduled) return;

        _updateScheduled = true;

        if (_dispatcher != null)
        {
            // Con dispatcher: batching inteligente en UI Thread
            _dispatcher.Post(() =>
            {
                _updateScheduled = false;
                try
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    // Ignore notification errors to prevent UI crashes
                }
            });
        }
        else
        {
            // Sin dispatcher: comportamiento actual (fallback para tests/otros frameworks)
            _updateScheduled = false;
            try
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Ignore notification errors
            }
        }
    }

    public void RaiseCanExecuteChangedSafe()
    {
        // Método legacy mantenido para compatibilidad
        RaiseCanExecuteChanged();
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
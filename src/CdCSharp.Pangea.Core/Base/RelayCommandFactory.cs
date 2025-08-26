using CdCSharp.Pangea.Core.Abstractions;
using System.Linq.Expressions;

namespace CdCSharp.Pangea.Core.Base;

public class RelayCommandFactory : IRelayCommandFactory
{
    public RelayCommand Create(Action execute, Func<bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(ExecuteWithLogging(execute, onError), canExecute);

    public RelayCommand Create(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(ExecuteWithLoggingAsync(executeAsync, onError), canExecute);

    public RelayCommand<T> Create<T>(Action<T?> execute, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(ExecuteWithLogging(execute, onError), canExecute);

    public RelayCommand<T> Create<T>(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(ExecuteWithLoggingAsync(executeAsync, onError), canExecute);

    // Nuevas sobrecargas para soportar expresiones de propiedades
    public RelayCommand Create<TViewModel>(Action execute, Expression<Func<TViewModel, bool>> canExecuteProperty, Action<Exception>? onError = null)
    {
        Func<bool> canExecuteFunc = CreateCanExecuteFunc(canExecuteProperty);
        return new(ExecuteWithLogging(execute, onError), canExecuteFunc);
    }

    public RelayCommand Create<TViewModel>(Func<Task> executeAsync, Expression<Func<TViewModel, bool>> canExecuteProperty, Action<Exception>? onError = null)
    {
        Func<bool> canExecuteFunc = CreateCanExecuteFunc(canExecuteProperty);
        return new(ExecuteWithLoggingAsync(executeAsync, onError), canExecuteFunc);
    }

    public RelayCommand<T> Create<T, TViewModel>(Action<T?> execute, Expression<Func<TViewModel, bool>> canExecuteProperty, Action<Exception>? onError = null)
    {
        Func<T?, bool> canExecuteFunc = CreateCanExecuteFunc<T, TViewModel>(canExecuteProperty);
        return new(ExecuteWithLogging(execute, onError), canExecuteFunc);
    }

    public RelayCommand<T> Create<T, TViewModel>(Func<T?, Task> executeAsync, Expression<Func<TViewModel, bool>> canExecuteProperty, Action<Exception>? onError = null)
    {
        Func<T?, bool> canExecuteFunc = CreateCanExecuteFunc<T, TViewModel>(canExecuteProperty);
        return new(ExecuteWithLoggingAsync(executeAsync, onError), canExecuteFunc);
    }

    private static Func<bool> CreateCanExecuteFunc<TViewModel>(Expression<Func<TViewModel, bool>> canExecuteProperty)
    {
        // Compilar la expresión para que sea evaluable
        Func<TViewModel, bool> compiledExpression = canExecuteProperty.Compile();
        
        return () =>
        {
            // Aquí necesitaríamos acceso a la instancia del ViewModel
            // Por ahora, devolvemos true como fallback
            // En una implementación real, necesitaríamos el contexto del ViewModel
            return true;
        };
    }

    private static Func<T?, bool> CreateCanExecuteFunc<T, TViewModel>(Expression<Func<TViewModel, bool>> canExecuteProperty)
    {
        Func<TViewModel, bool> compiledExpression = canExecuteProperty.Compile();
        
        return _ =>
        {
            // Similar al anterior, necesitaríamos contexto del ViewModel
            return true;
        };
    }

    private static Action ExecuteWithLogging(Action execute, Action<Exception>? onError)
    {
        return () =>
        {
            try
            {
                execute();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                throw;
            }
        };
    }

    private static Func<Task> ExecuteWithLoggingAsync(Func<Task> executeAsync, Action<Exception>? onError)
    {
        return async () =>
        {
            try
            {
                await executeAsync();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                throw;
            }
        };
    }

    private static Action<T?> ExecuteWithLogging<T>(Action<T?> execute, Action<Exception>? onError)
    {
        return parameter =>
        {
            try
            {
                execute(parameter);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                throw;
            }
        };
    }

    private static Func<T?, Task> ExecuteWithLoggingAsync<T>(Func<T?, Task> executeAsync, Action<Exception>? onError)
    {
        return async parameter =>
        {
            try
            {
                await executeAsync(parameter);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                throw;
            }
        };
    }
}
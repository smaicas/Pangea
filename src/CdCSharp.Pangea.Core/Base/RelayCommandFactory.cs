using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Core.Base;

public class RelayCommandFactory : IRelayCommandFactory
{
    public RelayCommand Create(Action execute, Func<bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(ExecuteWithLogging(execute, onError), canExecute);

    public RelayCommand Create(Func<Task> executeAsync, Func<bool>? canExecute = null,
        Action<Exception>? onError = null) =>
        new(ExecuteWithLoggingAsync(executeAsync, onError), canExecute);

    public RelayCommand<T> Create<T>(Action<T?> execute, Func<T?, bool>? canExecute = null,
        Action<Exception>? onError = null) =>
        new(param => Task.Run(() => ExecuteWithLogging(() => execute(param), onError)), canExecute);

    public RelayCommand<T> Create<T>(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null,
        Action<Exception>? onError = null) =>
        new(param => ExecuteWithLoggingAsync(() => executeAsync(param), onError), canExecute);

    private Action ExecuteWithLogging(Action execute, Action<Exception>? onError)
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

    private Func<Task> ExecuteWithLoggingAsync(Func<Task> executeAsync, Action<Exception>? onError)
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
}
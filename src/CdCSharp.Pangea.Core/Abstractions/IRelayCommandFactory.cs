using CdCSharp.Pangea.Core.Base;

namespace CdCSharp.Pangea.Core.Abstractions;

public interface IRelayCommandFactory
{
    RelayCommand Create(Action execute, Func<bool>? canExecute = null, Action<Exception>? onError = null);
    RelayCommand Create(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onError = null);
    RelayCommand<T> Create<T>(Action<T?> execute, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null);
    RelayCommand<T> Create<T>(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null);
}
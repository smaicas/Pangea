using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Core.Base;

/// <summary>
/// Builds commands wired to the application's UI dispatcher.
/// </summary>
/// <remarks>
/// The factory only supplies the ambient dispatcher and error sink; execution and error handling
/// belong to <see cref="RelayCommandBase"/>, so there is exactly one place where a command decides
/// what to do when its body throws.
/// </remarks>
public class RelayCommandFactory : IRelayCommandFactory
{
    private readonly IUIDispatcher? _dispatcher;

    public RelayCommandFactory(IUIDispatcher? dispatcher = null) => _dispatcher = dispatcher;

    public RelayCommand Create(Action execute, Func<bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(execute, canExecute, _dispatcher, onError);

    public RelayCommand Create(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(executeAsync, canExecute, _dispatcher, onError);

    public RelayCommand<T> Create<T>(Action<T?> execute, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(execute, canExecute, _dispatcher, onError);

    public RelayCommand<T> Create<T>(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null, Action<Exception>? onError = null) =>
        new(executeAsync, canExecute, _dispatcher, onError);
}

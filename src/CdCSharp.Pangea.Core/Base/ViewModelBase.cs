using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CdCSharp.Pangea.Core.Base;

public abstract class ViewModelBase : INotifyPropertyChanged, INavigationAware
{
    protected readonly IRelayCommandFactory CommandFactory;

    private readonly ConcurrentDictionary<(string Owner, MethodInfo Execute, MethodInfo? CanExecute), object> _commands = new();
    
    protected ViewModelBase(IServiceProvider serviceProvider) => 
        CommandFactory = serviceProvider.GetRequiredService<IRelayCommandFactory>();

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
    public virtual Task OnNavigatedFromAsync() => Task.CompletedTask;
    public virtual Task<bool> CanNavigateAwayAsync() => Task.FromResult(true);

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
            return build();
        }

        return (TCommand)_commands.GetOrAdd((owner, execute, canExecute), _ => build());
    }

    protected virtual void OnCommandError(Exception ex) { }
}
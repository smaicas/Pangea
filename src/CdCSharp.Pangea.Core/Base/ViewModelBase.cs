using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CdCSharp.Pangea.Core.Base;

public abstract class ViewModelBase : INotifyPropertyChanged, INavigationAware
{
    protected readonly IRelayCommandFactory CommandFactory;
    
    protected ViewModelBase(IServiceProvider serviceProvider) => 
        CommandFactory = serviceProvider.GetRequiredService<IRelayCommandFactory>();

    public virtual Task OnNavigatedToAsync(NavigationParameter? parameter) => Task.CompletedTask;
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

    protected RelayCommand CreateCommand(Action execute, Func<bool>? canExecute = null) =>
        CommandFactory.Create(execute, canExecute, OnCommandError);

    protected RelayCommand CreateCommand(Func<Task> executeAsync, Func<bool>? canExecute = null) =>
        CommandFactory.Create(executeAsync, canExecute, OnCommandError);

    protected RelayCommand<T> CreateCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) =>
        CommandFactory.Create(execute, canExecute, OnCommandError);

    protected RelayCommand<T> CreateCommand<T>(Func<T?, Task> executeAsync, Func<T?, bool>? canExecute = null) =>
        CommandFactory.Create(executeAsync, canExecute, OnCommandError);

    protected virtual void OnCommandError(Exception ex) { }
}
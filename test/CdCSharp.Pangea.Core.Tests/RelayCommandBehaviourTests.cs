using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Tests.Infrastructure;
using System.ComponentModel;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// CanExecute gating, re-entrancy and the notifications a bound control relies on.
/// </summary>
public class RelayCommandBehaviourTests
{
    [Fact]
    public async Task BodyDoesNotRun_WhenCanExecuteIsFalse()
    {
        bool ran = false;
        RelayCommand command = new(() => ran = true, canExecute: () => false);

        await command.ExecuteAsync();

        Assert.False(ran);
    }

    [Fact]
    public void CommandWithoutPredicate_IsAlwaysExecutable()
    {
        RelayCommand command = new(() => { });

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task WhileRunning_TheCommandReportsItselfAsNotExecutable()
    {
        TaskCompletionSource release = new();
        bool executableDuringRun = true;

        RelayCommand command = null!;
        command = new RelayCommand(async () =>
        {
            executableDuringRun = command.CanExecute(null);
            await release.Task;
        });

        Task running = command.ExecuteAsync();
        release.SetResult();
        await running;

        Assert.False(executableDuringRun, "Re-entrant execution must be blocked while the body runs.");
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task IsExecuting_RaisesPropertyChangedAndCanExecuteChanged()
    {
        List<string?> properties = new();
        int canExecuteChanges = 0;

        RelayCommand command = new(() => { });
        ((INotifyPropertyChanged)command).PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        command.CanExecuteChanged += (_, _) => canExecuteChanges++;

        await command.ExecuteAsync();

        // One transition into the run and one out of it.
        Assert.Equal(new[] { nameof(RelayCommand.IsExecuting), nameof(RelayCommand.IsExecuting) }, properties);
        Assert.Equal(2, canExecuteChanges);
    }

    [Fact]
    public void RaiseCanExecuteChanged_OnTheUIThread_FiresImmediately()
    {
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = true };
        int raised = 0;

        RelayCommand command = new(() => { }, dispatcher: dispatcher);
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
        Assert.Equal(0, dispatcher.PostCount);
    }

    [Fact]
    public void RaiseCanExecuteChanged_OffTheUIThread_IsMarshalledAndCoalesced()
    {
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = false, AutoFlushPosts = false };
        int raised = 0;

        RelayCommand command = new(() => { }, dispatcher: dispatcher);
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();
        command.RaiseCanExecuteChanged();
        command.RaiseCanExecuteChanged();

        Assert.Equal(1, dispatcher.PostCount);
        Assert.Equal(0, raised);

        dispatcher.FlushPosts();
        Assert.Equal(1, raised);

        // Once delivered, a further change schedules again rather than being dropped.
        command.RaiseCanExecuteChanged();
        Assert.Equal(2, dispatcher.PostCount);
    }

    [Theory]
    [InlineData("text", "text")]
    [InlineData(null, null)]
    [InlineData(42, "42")]
    public async Task TypedCommand_CoercesTheBindingParameter(object? parameter, string? expected)
    {
        string? received = null;
        RelayCommand<string> command = new(value => received = value);

        await command.ExecuteAsync(parameter);

        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task TypedCommand_UnconvertibleParameter_FallsBackToDefault()
    {
        int received = -1;
        RelayCommand<int> command = new(value => received = value);

        await command.ExecuteAsync("not a number");

        Assert.Equal(0, received);
    }

    [Fact]
    public void TypedCommand_PredicateSeesTheCoercedValue()
    {
        RelayCommand<int> command = new(_ => { }, canExecute: value => value > 10);

        Assert.True(command.CanExecute("42"));
        Assert.False(command.CanExecute("5"));
    }
}

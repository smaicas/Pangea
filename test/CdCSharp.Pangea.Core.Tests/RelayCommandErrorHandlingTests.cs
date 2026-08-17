using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Tests.Infrastructure;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// Failures always reach the error handler. Whether they also reach the caller depends on whether
/// the caller can act on them: ExecuteAsync rethrows, the ICommand entry point cannot.
/// </summary>
public class RelayCommandErrorHandlingTests
{
    [Fact]
    public void Execute_WhenBodyThrows_ReportsTheErrorAndDoesNotEscape()
    {
        Exception? reported = null;
        InvalidOperationException failure = new("boom");

        RelayCommand command = new(() => throw failure, onError: ex => reported = ex);

        // async void: an escaping exception would reach the process-wide handler and kill the app.
        command.Execute();

        Assert.Same(failure, reported);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBodyThrows_ReportsTheErrorAndRethrows()
    {
        Exception? reported = null;
        InvalidOperationException failure = new("boom");

        RelayCommand command = new(() => throw failure, onError: ex => reported = ex);

        InvalidOperationException thrown =
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());

        Assert.Same(failure, thrown);
        Assert.Same(failure, reported);
    }

    [Fact]
    public async Task FailingBody_StillClearsIsExecuting()
    {
        RelayCommand command = new(() => throw new InvalidOperationException(), onError: _ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());

        Assert.False(command.IsExecuting, "A failed run must not leave the command permanently disabled.");
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void ThrowingCanExecute_IsReportedAndTreatedAsFalse()
    {
        Exception? reported = null;
        RelayCommand command = new(
            () => { },
            canExecute: () => throw new InvalidOperationException("predicate"),
            onError: ex => reported = ex);

        // Bindings evaluate CanExecute; letting it throw would break the control, not just the command.
        bool canExecute = command.CanExecute(null);

        Assert.False(canExecute);
        Assert.NotNull(reported);
    }

    [Fact]
    public async Task ErrorsFromAnAsyncBody_AreReportedToo()
    {
        Exception? reported = null;

        RelayCommand command = new(
            () => Task.FromException(new TimeoutException()),
            onError: ex => reported = ex);

        await Assert.ThrowsAsync<TimeoutException>(() => command.ExecuteAsync());

        Assert.IsType<TimeoutException>(reported);
    }

    [Fact]
    public void WithoutAnErrorHandler_ExecuteStillDoesNotThrow()
    {
        RelayCommand command = new(() => throw new InvalidOperationException());

        command.Execute();
    }

    [Fact]
    public void NullBody_IsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action)null!));
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Func<Task>)null!));
        Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>((Action<string?>)null!));
    }

    [Fact]
    public async Task DispatcherFailures_SurfaceInsteadOfBeingSwallowed()
    {
        // The dispatcher must not hide a failing body; the command decides what to do about it.
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = false };
        Exception? reported = null;

        RelayCommand command = new(
            () => throw new InvalidOperationException("inside invoke"),
            dispatcher: dispatcher,
            onError: ex => reported = ex);

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync());

        Assert.NotNull(reported);
    }
}

using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Tests.Infrastructure;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// The factory's only job is to hand every command the ambient dispatcher and error sink.
/// </summary>
public class RelayCommandFactoryTests
{
    [Fact]
    public async Task CreatedCommands_UseTheAmbientDispatcher()
    {
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = false };
        RelayCommandFactory factory = new(dispatcher);

        await factory.Create(() => { }).ExecuteAsync();

        Assert.Equal(1, dispatcher.InvokeCount);
    }

    [Fact]
    public void CreatedCommands_RouteFailuresToTheSuppliedHandler()
    {
        Exception? reported = null;
        RelayCommandFactory factory = new();

        factory.Create(() => throw new InvalidOperationException(), onError: ex => reported = ex).Execute();

        Assert.NotNull(reported);
    }

    [Fact]
    public async Task TypedCommands_AlsoCarryDispatcherAndErrorHandler()
    {
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = false };
        Exception? reported = null;
        RelayCommandFactory factory = new(dispatcher);

        // Explicit delegate type: a throw-only lambda binds to both the Action and the Func<Task>
        // overload, and the sync one is what this test is about.
        Action<string?> body = _ => throw new InvalidOperationException();
        RelayCommand<string> command = factory.Create(body, onError: ex => reported = ex);

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync("x"));

        Assert.Equal(1, dispatcher.InvokeCount);
        Assert.NotNull(reported);
    }

    [Fact]
    public async Task AsyncOverloads_AreNotWrappedInABackgroundTask()
    {
        RelayCommandFactory factory = new();
        int callingThread = Environment.CurrentManagedThreadId;
        int bodyThread = -1;

        await factory.Create(() =>
        {
            bodyThread = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        }).ExecuteAsync();

        Assert.Equal(callingThread, bodyThread);
    }
}

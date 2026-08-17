using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Tests.Infrastructure;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// A synchronous command body has to end up on the UI thread: it almost always touches the view
/// model the UI is bound to, and Avalonia rejects cross-thread access.
/// </summary>
public class RelayCommandThreadingTests
{
    [Fact]
    public async Task SyncBody_CalledFromBackgroundThread_IsMarshalledToTheUIThread()
    {
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = false };
        bool ranOnUIThread = false;

        RelayCommand command = new(() => ranOnUIThread = dispatcher.InvokeCount == 1, dispatcher: dispatcher);

        await command.ExecuteAsync();

        Assert.Equal(1, dispatcher.InvokeCount);
        Assert.True(ranOnUIThread, "The body must run inside the dispatcher's Invoke, not before it.");
    }

    [Fact]
    public async Task SyncBody_CalledOnTheUIThread_RunsInlineWithoutMarshalling()
    {
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = true };
        bool ran = false;

        RelayCommand command = new(() => ran = true, dispatcher: dispatcher);

        await command.ExecuteAsync();

        Assert.True(ran);
        Assert.Equal(0, dispatcher.InvokeCount);
    }

    [Fact]
    public async Task SyncBody_RunsOnTheCallingThread_NotOnAThreadPoolThread()
    {
        // Without a dispatcher there is nothing to marshal to, so the body must still run inline.
        int callingThread = Environment.CurrentManagedThreadId;
        int bodyThread = -1;

        RelayCommand command = new(() => bodyThread = Environment.CurrentManagedThreadId);

        await command.ExecuteAsync();

        Assert.Equal(callingThread, bodyThread);
    }

    [Fact]
    public async Task AsyncBody_IsNotPushedOntoAThreadPoolThread()
    {
        int callingThread = Environment.CurrentManagedThreadId;
        int bodyThread = -1;

        RelayCommand command = new(() =>
        {
            bodyThread = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        });

        await command.ExecuteAsync();

        Assert.Equal(callingThread, bodyThread);
    }

    [Fact]
    public async Task TypedSyncBody_IsMarshalledToo()
    {
        FakeUIDispatcher dispatcher = new() { IsOnUIThread = false };
        string? received = null;

        RelayCommand<string> command = new(value => received = value, dispatcher: dispatcher);

        await command.ExecuteAsync("payload");

        Assert.Equal(1, dispatcher.InvokeCount);
        Assert.Equal("payload", received);
    }
}

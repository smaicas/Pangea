using CdCSharp.Pangea.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using PangeaSupabaseApp.Data;
using PangeaSupabaseApp.Services;

namespace PangeaSupabaseApp.Tests;

/// <summary>
/// What the template is actually for: an application that keeps working in a tunnel.
/// </summary>
/// <remarks>
/// None of this is visible by running the application - you would have to turn the network off at
/// the right moment and turn it back on at another - which is exactly why it is the part worth
/// having tests for.
/// </remarks>
public class NotesRepositoryTests
{
    [Fact]
    public async Task AWriteMadeOffline_IsShownAndKept()
    {
        Fixture fixture = new();
        fixture.Backend.GoOffline();

        await fixture.Repository.AddAsync("Buy milk", TestContext.Current.CancellationToken);

        // Shown: the screen draws the cache, and the note is in it.
        Assert.Contains(await fixture.Repository.AllAsync(TestContext.Current.CancellationToken), note => note.Title == "Buy milk");

        // Kept: nothing reached the server, and the write is waiting rather than lost.
        Assert.Empty(fixture.Backend.Stored);
        Assert.Equal(1, await fixture.Repository.PendingAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenTheNetworkComesBack_TheQueuedWriteLands()
    {
        Fixture fixture = new();
        fixture.Backend.GoOffline();

        await fixture.Repository.AddAsync("Buy milk", TestContext.Current.CancellationToken);

        fixture.Backend.ComeBack();

        int sent = await fixture.Repository.SyncAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, sent);
        Assert.Contains(fixture.Backend.Stored, note => note.Title == "Buy milk");
        Assert.Equal(0, await fixture.Repository.PendingAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A refusal is not a queue, and it is not silence either.
    /// </summary>
    /// <remarks>
    /// The server will say the same thing next time, so queueing it would put an entry in front of
    /// every later write that can never be accepted. It is raised instead of swallowed, which is
    /// what puts the message in front of the user: <c>ViewModelBase</c> catches what a command
    /// throws and reports it.
    /// </remarks>
    [Fact]
    public async Task AWriteTheServerRefuses_ReachesTheCallerAndIsNotQueued()
    {
        Fixture fixture = new();
        fixture.Backend.Refuse();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Repository.AddAsync("Not allowed", TestContext.Current.CancellationToken));

        Assert.Equal(0, await fixture.Repository.PendingAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARefreshThatFails_LeavesTheCachedListStanding()
    {
        Fixture fixture = new();

        await fixture.Repository.AddAsync("Buy milk", TestContext.Current.CancellationToken);

        fixture.Backend.GoOffline();

        Assert.Contains(await fixture.Repository.RefreshAsync(TestContext.Current.CancellationToken), note => note.Title == "Buy milk");
    }

    /// <summary>The repository with a fake server, a queue in memory and a cache on no disk.</summary>
    private sealed class Fixture
    {
        public Fixture()
        {
            PangeaTestServices services = new();

            NotesCache cache = new(services.Storage, NullLogger<NotesCache>.Instance);

            Repository = new NotesRepository(Backend, cache, Outbox, NullLogger<NotesRepository>.Instance);
        }

        public FakeBackend Backend { get; } = new();

        public InMemoryOutbox Outbox { get; } = new();

        public NotesRepository Repository { get; }
    }
}

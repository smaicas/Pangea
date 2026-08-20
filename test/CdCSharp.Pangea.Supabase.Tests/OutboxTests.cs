using CdCSharp.Pangea.Supabase.Abstractions;
using CdCSharp.Pangea.Supabase.Services;
using CdCSharp.Pangea.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Supabase.Tests;

/// <summary>
/// The writes made while the backend was out of reach.
/// </summary>
/// <remarks>
/// The cases that matter are the ones a phone actually produces: a queue that outlives the process,
/// a replay that fails halfway, and an entry that has been tried more than once.
/// </remarks>
public class OutboxTests
{
    /// <summary>The suite's token, so a cancelled run stops these rather than waiting them out.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (FileOutbox Outbox, InMemoryStorageService Storage) Arrange(
        InMemoryStorageService? storage = null)
    {
        InMemoryStorageService files = storage ?? new InMemoryStorageService();

        return (new FileOutbox(files, Options.Create(new SupabaseOptions()), NullLogger<FileOutbox>.Instance), files);
    }

    [Fact]
    public async Task AnEmptyOutboxHasNothingPending()
    {
        (FileOutbox outbox, _) = Arrange();

        Assert.Empty(await outbox.PendingAsync(Ct));
    }

    [Fact]
    public async Task WhatIsEnqueuedComesBackInTheOrderItWasMade()
    {
        (FileOutbox outbox, _) = Arrange();

        await outbox.EnqueueAsync("expense.add", "first", Ct);
        await outbox.EnqueueAsync("expense.add", "second", Ct);
        await outbox.EnqueueAsync("expense.delete", "third", Ct);

        Assert.Equal(["first", "second", "third"], (await outbox.PendingAsync(Ct)).Select(entry => entry.Payload));
    }

    [Fact]
    public async Task AnEntryIsGivenAnIdAndATime()
    {
        (FileOutbox outbox, _) = Arrange();

        OutboxEntry entry = await outbox.EnqueueAsync("expense.add", "{}", Ct);

        Assert.NotEmpty(entry.Id);
        Assert.Equal(0, entry.Attempts);
        Assert.True(entry.QueuedAt <= DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// The whole point: the application was closed on the train and the write is still there when it
    /// opens on the platform.
    /// </summary>
    [Fact]
    public async Task TheQueueSurvivesTheProcessThatWroteIt()
    {
        (FileOutbox first, InMemoryStorageService files) = Arrange();

        await first.EnqueueAsync("expense.add", "supermarket", Ct);

        (FileOutbox second, _) = Arrange(files);

        OutboxEntry restored = Assert.Single(await second.PendingAsync(Ct));
        Assert.Equal("supermarket", restored.Payload);
    }

    [Fact]
    public async Task DrainingRemovesWhatTheHandlerAccepted()
    {
        (FileOutbox outbox, _) = Arrange();

        await outbox.EnqueueAsync("expense.add", "one", Ct);
        await outbox.EnqueueAsync("expense.add", "two", Ct);

        int accepted = await outbox.DrainAsync((_, _) => Task.FromResult(true), Ct);

        Assert.Equal(2, accepted);
        Assert.Empty(await outbox.PendingAsync(Ct));
    }

    /// <summary>
    /// Order is the reason a drain stops rather than skipping: a later write applied on top of a
    /// missing earlier one is worse than a queue that waits.
    /// </summary>
    [Fact]
    public async Task ADrainStopsAtTheFirstWriteThatDidNotLand()
    {
        (FileOutbox outbox, _) = Arrange();

        await outbox.EnqueueAsync("expense.add", "one", Ct);
        await outbox.EnqueueAsync("expense.add", "two", Ct);
        await outbox.EnqueueAsync("expense.add", "three", Ct);

        List<string> seen = [];

        int accepted = await outbox.DrainAsync((entry, _) =>
        {
            seen.Add(entry.Payload);
            return Task.FromResult(entry.Payload == "one");
        }, Ct);

        Assert.Equal(1, accepted);
        Assert.Equal(["one", "two"], seen);
        Assert.Equal(["two", "three"], (await outbox.PendingAsync(Ct)).Select(entry => entry.Payload));
    }

    [Fact]
    public async Task AWriteThatDidNotLandRecordsTheAttempt()
    {
        (FileOutbox outbox, _) = Arrange();

        await outbox.EnqueueAsync("expense.add", "one", Ct);

        await outbox.DrainAsync((_, _) => Task.FromResult(false), Ct);
        await outbox.DrainAsync((_, _) => Task.FromResult(false), Ct);

        Assert.Equal(2, (await outbox.PendingAsync(Ct))[0].Attempts);
    }

    /// <summary>
    /// A handler that throws is a handler that failed. Letting it out would abandon the drain with
    /// the entry neither applied nor counted.
    /// </summary>
    [Fact]
    public async Task AHandlerThatThrowsLeavesTheEntryQueued()
    {
        (FileOutbox outbox, _) = Arrange();

        await outbox.EnqueueAsync("expense.add", "one", Ct);

        int accepted = await outbox.DrainAsync((_, _) => throw new HttpRequestException("no route to host"), Ct);

        Assert.Equal(0, accepted);
        Assert.Single(await outbox.PendingAsync(Ct));
    }

    /// <summary>Cancelling a drain is not the same as a write failing, and must not be counted as one.</summary>
    [Fact]
    public async Task CancellingADrainDoesNotCountAsAFailedAttempt()
    {
        (FileOutbox outbox, _) = Arrange();

        await outbox.EnqueueAsync("expense.add", "one", Ct);

        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => outbox.DrainAsync((_, token) => Task.FromCanceled<bool>(token), cancelled.Token));

        Assert.Equal(0, (await outbox.PendingAsync(Ct))[0].Attempts);
    }

    [Fact]
    public async Task AnEntryCanBeDroppedWithoutBeingReplayed()
    {
        (FileOutbox outbox, _) = Arrange();

        OutboxEntry dropped = await outbox.EnqueueAsync("expense.add", "one", Ct);
        await outbox.EnqueueAsync("expense.add", "two", Ct);

        await outbox.RemoveAsync(dropped.Id, Ct);

        Assert.Equal("two", Assert.Single(await outbox.PendingAsync(Ct)).Payload);
    }

    [Fact]
    public async Task ClearingEmptiesTheQueue()
    {
        (FileOutbox outbox, _) = Arrange();

        await outbox.EnqueueAsync("expense.add", "one", Ct);
        await outbox.ClearAsync(Ct);

        Assert.Empty(await outbox.PendingAsync(Ct));
    }

    /// <summary>
    /// A corrupt queue is reported and treated as empty. Throwing from every write afterwards would
    /// turn one unreadable file into an application that cannot be used, and the entries are gone
    /// either way.
    /// </summary>
    [Fact]
    public async Task AnUnreadableQueueIsTreatedAsEmpty()
    {
        InMemoryStorageService files = new();
        (FileOutbox outbox, _) = Arrange(files);

        await files.WriteTextAsync(files.GetDataFilePath(new SupabaseOptions().OutboxFileName), "not json");

        Assert.Empty(await outbox.PendingAsync(Ct));

        // And still usable: the next write goes in and comes back.
        await outbox.EnqueueAsync("expense.add", "after", Ct);
        Assert.Single(await outbox.PendingAsync(Ct));
    }

    [Fact]
    public async Task EnqueueingRejectsAKindThatSaysNothing()
    {
        (FileOutbox outbox, _) = Arrange();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => outbox.EnqueueAsync("  ", "{}", Ct));
    }
}

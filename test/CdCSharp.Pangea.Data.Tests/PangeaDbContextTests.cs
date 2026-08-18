using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.ObjectModel;

namespace CdCSharp.Pangea.Data.Tests;

/// <summary>
/// How a view model reaches the database: a context per operation, nothing tracked between them,
/// and writes that survive being made at once.
/// </summary>
public class PangeaDbContextTests
{
    [Fact]
    public async Task AWriteIsSavedWithoutTheCallerSayingSo()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        int written = await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "first" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(1, written);

        List<Note> notes = await database.Db.ReadAsync(
            (context, token) => context.Notes.ToListAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal("first", Assert.Single(notes).Title);
    }

    [Fact]
    public async Task AWriteThatProducesAValue_ReturnsItAfterSaving()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Note saved = await database.Db.WriteAsync(async (context, token) =>
        {
            Note note = new() { Title = "identified" };
            await context.Notes.AddAsync(note, token);
            return note;
        }, TestContext.Current.CancellationToken);

        // The identity is assigned by the insert, so a non-zero one is proof the save happened
        // before the value came back rather than after.
        Assert.True(saved.Id > 0);
    }

    /// <summary>
    /// What a read hands back is data, not a live entity. Anything else would keep a graph alive
    /// behind a context that has already been disposed, and write it back on the next save.
    /// </summary>
    [Fact]
    public async Task WhatAReadReturnsIsNotTracked()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "unchanged" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Note note = await database.Db.ReadAsync(
            (context, token) => context.Notes.FirstAsync(token), TestContext.Current.CancellationToken);

        note.Title = "changed in memory";

        Note reread = await database.Db.ReadAsync(
            (context, token) => context.Notes.FirstAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal("unchanged", reread.Title);
    }

    /// <summary>
    /// SQLite has one writer. Without the feature taking writes one at a time, this is the test
    /// that fails with "database is locked" - on someone else's machine, under load, once.
    /// </summary>
    [Fact]
    public async Task ManyWritesAtOnce_AllLand()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        const int writers = 20;

        await Task.WhenAll(Enumerable.Range(0, writers).Select(index =>
            database.Db.WriteAsync((context, _) =>
            {
                context.Notes.Add(new Note { Title = $"note {index}" });
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken)));

        int count = await database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal(writers, count);
    }

    [Fact]
    public async Task AQueryCanComeBackReadyToBindTo()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.AddRange(new Note { Title = "a" }, new Note { Title = "b" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        ObservableCollection<string> titles = await database.Db.ToObservableAsync(
            context => context.Notes.OrderBy(note => note.Title).Select(note => note.Title),
            TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b"], titles);
    }

    [Fact]
    public async Task AContextOfYourOwn_IsHandedOutForTheWorkTheseMethodsDoNotCover()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await using (NotesContext context = database.Db.Create())
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

            context.Notes.Add(new Note { Title = "transacted" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheHarnessDeletesItsDatabaseWithTheTest()
    {
        string directory;

        await using (PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            directory = database.DirectoryPath;
            Assert.True(Directory.Exists(directory));
        }

        Assert.False(Directory.Exists(directory));
    }
}

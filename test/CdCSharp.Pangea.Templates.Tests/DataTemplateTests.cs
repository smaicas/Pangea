using CdCSharp.Pangea.Data;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Testing;
using CdCSharp.Pangea.Testing;
using Microsoft.EntityFrameworkCore;
using PangeaDataApp.Data;
using PangeaDataApp.ViewModels;
using System.Diagnostics;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// The data template's screen, driven against a real SQLite database.
/// </summary>
/// <remarks>
/// The database is built with <see cref="MigrationStrategy.Migrate"/> rather than from the model, so
/// the migration the template ships is applied here exactly as it will be on a user's machine. A
/// migration that has drifted from the model is otherwise invisible until someone runs the
/// generated application.
/// </remarks>
public class DataTemplateTests
{
    private static Task<PangeaTestDatabase<AppDbContext>> DatabaseAsync(CancellationToken cancellationToken) =>
        PangeaTestDatabase<AppDbContext>.CreateAsync(
            db => db.Options.Migration = MigrationStrategy.Migrate,
            services => services.AddDataSeeder<AppDbContext, WelcomeNoteSeeder>(),
            cancellationToken);

    private static MainWindowViewModel Screen(PangeaTestDatabase<AppDbContext> database)
    {
        PangeaTestServices services = new();
        services.Add(database.Db);
        services.Add(database.Maintenance);

        return new MainWindowViewModel(services);
    }

    /// <summary>
    /// The view model starts its first load in its constructor, which is what a screen with no
    /// navigation to hang it off has to do. Nothing else can be asserted until it has finished.
    /// </summary>
    private static async Task IdleAsync(MainWindowViewModel screen)
    {
        Stopwatch elapsed = Stopwatch.StartNew();

        while (screen.IsBusy && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.False(screen.IsBusy, "The view model was still busy after ten seconds.");
    }

    [Fact]
    public async Task TheShippedMigrationBuildsTheSchemaTheModelExpects()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await DatabaseAsync(TestContext.Current.CancellationToken);

        // Reading through the model over a schema the migration wrote is what proves the two agree.
        List<Note> notes = await database.Db.ReadAsync(
            (context, token) => context.Notes.ToListAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal("Welcome", Assert.Single(notes).Title);
    }

    [Fact]
    public async Task TheScreenShowsWhatTheSeederWrote_AndDescribesTheDatabase()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await DatabaseAsync(TestContext.Current.CancellationToken);

        MainWindowViewModel screen = Screen(database);
        await IdleAsync(screen);

        Assert.True(screen.HasNotes);
        Assert.Equal("Welcome", Assert.Single(screen.Notes).Title);
        Assert.Contains("SQLite", screen.DatabaseSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddingANote_SavesItAndClearsTheForm()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await DatabaseAsync(TestContext.Current.CancellationToken);

        MainWindowViewModel screen = Screen(database);
        await IdleAsync(screen);

        screen.NewTitle = "Second";
        await screen.AddNoteCommand.ExecuteAsync();

        Assert.Equal("", screen.NewTitle);
        Assert.Contains(screen.Notes, note => note.Title == "Second");

        Assert.Equal(2, await database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ATitleThatFailsValidation_IsNotSaved()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await DatabaseAsync(TestContext.Current.CancellationToken);

        MainWindowViewModel screen = Screen(database);
        await IdleAsync(screen);

        screen.NewTitle = "x";
        await screen.AddNoteCommand.ExecuteAsync();

        Assert.True(screen.HasErrors);

        Assert.Equal(1, await database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeletingTheSelectedNote_RemovesIt()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await DatabaseAsync(TestContext.Current.CancellationToken);

        MainWindowViewModel screen = Screen(database);
        await IdleAsync(screen);

        screen.SelectedNote = screen.Notes[0];
        await screen.DeleteNoteCommand.ExecuteAsync();

        Assert.False(screen.HasNotes);

        Assert.Equal(0, await database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BackingUpReportsWhereItWent()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await DatabaseAsync(TestContext.Current.CancellationToken);

        MainWindowViewModel screen = Screen(database);
        await IdleAsync(screen);

        await screen.BackupCommand.ExecuteAsync();

        string backup = Assert.Single(database.Maintenance.GetBackups());

        Assert.True(File.Exists(backup));
        Assert.Contains(backup, screen.Status, StringComparison.Ordinal);
    }
}

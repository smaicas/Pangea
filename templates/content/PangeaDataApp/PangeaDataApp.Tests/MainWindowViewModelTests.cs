using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Testing;
using CdCSharp.Pangea.Testing;
using Microsoft.EntityFrameworkCore;
using PangeaDataApp.Data;
using PangeaDataApp.ViewModels;
using System.Diagnostics;

namespace PangeaDataApp.Tests;

/// <summary>
/// The screen, driven against a real SQLite database.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PangeaTestDatabase{TContext}"/> builds one from the migrations this project ships and
/// deletes it afterwards. Built with <see cref="MigrationStrategy.Migrate"/> on purpose: creating
/// the schema from the model instead would skip the migration, which is the half most likely to
/// have drifted, and the drift is invisible until it fails on somebody's machine.
/// </para>
/// <para>
/// Everything under <c>Domain</c> is tested without any of this - see DomainTests. Reach for a
/// database when the question really is about the database.
/// </para>
/// </remarks>
public class MainWindowViewModelTests
{
    [Fact]
    public async Task AddingANote_LandsInTheDatabaseAndClearsTheForm()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await PangeaTestDatabase<AppDbContext>.CreateAsync(
                db => db.Options.Migration = MigrationStrategy.Migrate,
                cancellationToken: TestContext.Current.CancellationToken);

        MainWindowViewModel screen = Screen(database);
        await IdleAsync(screen);

        screen.NewTitle = "  Shopping list  ";
        screen.NewBody = "   ";

        screen.AddNoteCommand.Execute(null);
        await IdleAsync(screen);

        Note saved = await database.Db.ReadAsync(
            (context, token) => context.Notes.SingleAsync(note => note.Title == "Shopping list", token),
            TestContext.Current.CancellationToken);

        // Trimmed, and a body of spaces stored as null: the rules from Domain\NoteDraft, reaching
        // the table through the screen.
        Assert.Null(saved.Body);
        Assert.Equal("", screen.NewTitle);
    }

    private static MainWindowViewModel Screen(PangeaTestDatabase<AppDbContext> database)
    {
        PangeaTestServices services = new();

        services.Add(database.Db);
        services.Add(database.Maintenance);

        return new MainWindowViewModel(services);
    }

    /// <summary>
    /// The screen starts its first load in its constructor, which is what a screen with no
    /// navigation to hang it off has to do. Nothing else can be asserted until it has finished.
    /// </summary>
    private static async Task IdleAsync(MainWindowViewModel screen)
    {
        Stopwatch elapsed = Stopwatch.StartNew();

        while (screen.IsBusy && elapsed.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.False(screen.IsBusy, "The screen was still busy after ten seconds.");
    }
}

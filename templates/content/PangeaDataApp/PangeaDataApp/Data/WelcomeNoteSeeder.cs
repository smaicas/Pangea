using CdCSharp.Pangea.Data.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace PangeaDataApp.Data;

/// <summary>
/// Puts something in an empty database so the first run has something to show.
/// </summary>
/// <remarks>
/// Seeders run at startup after the schema is up to date - on every run, not only the first - so
/// the check below is not optional. Whatever is left pending on the context is saved for you.
/// </remarks>
public sealed class WelcomeNoteSeeder : IDataSeeder<AppDbContext>
{
    public async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Notes.AnyAsync(cancellationToken)) return;

        context.Notes.Add(new Note
        {
            Title = "Welcome",
            Body = "This note came from WelcomeNoteSeeder, which runs once because the table was empty."
        });
    }
}

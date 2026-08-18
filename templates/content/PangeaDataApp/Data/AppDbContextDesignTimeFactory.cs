using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PangeaDataApp.Data;

/// <summary>
/// How <c>dotnet ef</c> builds the context when it writes a migration.
/// </summary>
/// <remarks>
/// <para>
/// The tooling starts the application to find a context, which an Avalonia application answers by
/// opening a window. This factory is what it looks for first, so it never gets that far.
/// </para>
/// <para>
/// The connection string here is only ever used to work out the SQL dialect - a migration is
/// scaffolded from the model, and nothing is written to this file. The database the application
/// actually uses is the one <c>App.Configure</c> describes.
/// </para>
/// <para>
/// Add a migration after changing the model:
/// <code>
/// dotnet tool install --global dotnet-ef
/// dotnet ef migrations add AddSomething --output-dir Data/Migrations
/// </code>
/// There is no <c>database update</c> step: the application applies its own migrations at startup.
/// </para>
/// </remarks>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options);
}

using Microsoft.EntityFrameworkCore;

namespace CdCSharp.Pangea.Data.Abstractions;

/// <summary>
/// Data the application needs in the database before it can show anything useful.
/// </summary>
/// <remarks>
/// Every seeder registered for <typeparamref name="TContext"/> runs at startup, in
/// <see cref="Order"/>, after the schema is up to date - on every run, not only the first. A seeder
/// is responsible for its own idempotence: check before you insert.
/// </remarks>
public interface IDataSeeder<TContext> where TContext : DbContext
{
    /// <summary>Lower runs first.</summary>
    int Order => 0;

    Task SeedAsync(TContext context, CancellationToken cancellationToken);
}

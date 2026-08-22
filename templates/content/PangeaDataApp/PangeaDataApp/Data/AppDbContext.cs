using Microsoft.EntityFrameworkCore;

namespace PangeaDataApp.Data;

/// <summary>The application's database.</summary>
/// <remarks>
/// <para>
/// The constructor taking <see cref="DbContextOptions{TContext}"/> is the one the factory uses, and
/// the only one this needs: where the file lives and how it is opened were decided in
/// <c>App.Configure</c>, not here. A context that configured itself would ignore all of it.
/// </para>
/// <para>
/// Nothing resolves this from the container. Ask for <c>IPangeaDbContext&lt;AppDbContext&gt;</c>
/// instead, which builds one per operation and disposes it.
/// </para>
/// </remarks>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>(note =>
        {
            note.HasKey(entity => entity.Id);
            note.Property(entity => entity.Title).IsRequired().HasMaxLength(80);
            note.Property(entity => entity.Body).HasMaxLength(2000);
            note.Property(entity => entity.CreatedUtc).IsRequired();

            // The list is ordered by this, and an index is cheaper than sorting the table every
            // time the window opens.
            note.HasIndex(entity => entity.CreatedUtc);
        });
    }
}

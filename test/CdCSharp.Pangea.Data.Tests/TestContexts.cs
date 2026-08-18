using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CdCSharp.Pangea.Data.Tests;

public class Note
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
}

/// <summary>A context with no migrations, created from the model.</summary>
public class NotesContext : DbContext
{
    public NotesContext(DbContextOptions<NotesContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
}

/// <summary>A context whose schema comes from a migration that works.</summary>
public class MigratedContext : DbContext
{
    public MigratedContext(DbContextOptions<MigratedContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
}

/// <summary>
/// A context whose only migration fails. Migrations are found by the context they are attributed
/// to, so this one's failure cannot reach any other context in this assembly.
/// </summary>
public class BrokenMigrationContext : DbContext
{
    public BrokenMigrationContext(DbContextOptions<BrokenMigrationContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
}

/// <summary>
/// Written by hand rather than scaffolded. A migration is a class with an Up and a Down; the model
/// snapshot beside a generated one exists for the tooling that writes the next migration, and there
/// is no next one here.
/// </summary>
[DbContext(typeof(MigratedContext))]
[Migration("0001_Initial")]
public class MigratedContextInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notes",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                Title = table.Column<string>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Notes", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Notes");
}

[DbContext(typeof(BrokenMigrationContext))]
[Migration("0001_Broken")]
public class BrokenMigrationContextInitial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("SELECT this_function_does_not_exist();");

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}

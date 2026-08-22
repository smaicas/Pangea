namespace PangeaDataApp.Data;

/// <summary>One row of the notes table.</summary>
/// <remarks>
/// A plain class with no attributes: the mapping is declared in
/// <see cref="AppDbContext.OnModelCreating"/>, which keeps the shape of the table in one place and
/// leaves the entity readable.
/// </remarks>
public class Note
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

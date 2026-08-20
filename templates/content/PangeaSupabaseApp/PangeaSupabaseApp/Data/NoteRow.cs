using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace PangeaSupabaseApp.Data;

/// <summary>
/// The table, as the client sees it.
/// </summary>
/// <remarks>
/// Deliberately not the type the rest of the application reasons about. This one is shaped by
/// Postgres - flat, snake_case - and letting it be the domain type would put a Supabase attribute
/// in the middle of the application and make every schema change reach into it.
/// </remarks>
[Table("notes")]
public class NoteRow : BaseModel
{
    /// <summary>
    /// Assigned by the client, not the database.
    /// </summary>
    /// <remarks>
    /// The second argument is whether the client sends the value. True here, because a note written
    /// offline has to keep the identity it was given: it is what lets the same row be sent twice
    /// without becoming two notes. A column with a database default wants false, or the client
    /// sends an empty Guid and overwrites it.
    /// </remarks>
    [PrimaryKey("id", true)] public Guid Id { get; set; }

    [Column("owner_id")] public string OwnerId { get; set; } = "";

    [Column("title")] public string Title { get; set; } = "";

    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

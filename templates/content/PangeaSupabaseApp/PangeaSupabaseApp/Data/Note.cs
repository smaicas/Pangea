namespace PangeaSupabaseApp.Data;

/// <summary>One note, as the application means it.</summary>
/// <param name="Id">Assigned by the client, so a note written offline keeps its identity.</param>
/// <param name="Title">What the user typed.</param>
/// <param name="CreatedAt">When it was written, on the device that wrote it.</param>
public sealed record Note(Guid Id, string Title, DateTimeOffset CreatedAt);

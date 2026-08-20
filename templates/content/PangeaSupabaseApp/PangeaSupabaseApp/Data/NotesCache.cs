using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Logging;

namespace PangeaSupabaseApp.Data;

/// <summary>
/// The last thing the application knew, kept on the device.
/// </summary>
/// <remarks>
/// What the screen draws before the network has said anything, and what it keeps drawing when the
/// network never does. A list that arrives half a second after the screen is a list nobody trusts.
/// </remarks>
public sealed class NotesCache
{
    private const string FileName = "notes.json";

    private readonly IStorageService _storage;
    private readonly ILogger<NotesCache> _logger;

    public NotesCache(IStorageService storage, ILogger<NotesCache> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Note>> ReadAsync()
    {
        string path = _storage.GetDataFilePath(FileName);

        if (!_storage.FileExists(path)) return [];

        try
        {
            return await _storage.ReadJsonAsync<List<Note>>(path).ConfigureAwait(false) ?? [];
        }
        catch (Exception ex)
        {
            // Unreadable is the same as absent: the server has the truth, and refusing to open the
            // screen over a bad file would be the worst of both.
            _logger.LogWarning(ex, "The cache at {Path} could not be read", path);
            return [];
        }
    }

    public async Task WriteAsync(IReadOnlyList<Note> notes)
    {
        try
        {
            await _storage.WriteJsonAsync(_storage.GetDataFilePath(FileName), notes).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A cache that could not be written costs the next launch its instant screen and
            // nothing else. Logged rather than swallowed: a serialization failure here is a bug in
            // the model, not a disk problem, and it is invisible otherwise.
            _logger.LogError(ex, "The cache could not be written");
        }
    }
}

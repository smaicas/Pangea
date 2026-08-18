using CdCSharp.Pangea.Storage.Abstractions;

namespace PangeaShellApp.Services;

/// <summary>What the application remembers between runs.</summary>
public sealed class AppSettings
{
    public string Culture { get; set; } = "en-US";

    public bool IsDark { get; set; }
}

/// <summary>
/// Reads and writes <c>settings.json</c> in the per-user data directory for this platform.
/// </summary>
/// <remarks>
/// The path comes from <see cref="IStorageService"/> rather than from the application: it is
/// <c>%APPDATA%</c> on Windows, <c>~/.config</c> on Linux and <c>~/Library/Application Support</c>
/// on macOS, and the storage feature already knows which one it is running on.
/// </remarks>
public sealed class AppSettingsStore
{
    private const string FileName = "settings.json";

    private readonly IStorageService _storage;

    public AppSettingsStore(IStorageService storage) => _storage = storage;

    /// <summary>The saved settings, or the defaults on a first run or an unreadable file.</summary>
    public async Task<AppSettings> LoadAsync()
    {
        string path = _storage.GetDataFilePath(FileName);

        if (!_storage.FileExists(path)) return new AppSettings();

        try
        {
            return await _storage.ReadJsonAsync<AppSettings>(path) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience: an unreadable file is worth starting without, not
            // worth refusing to start over.
            return new AppSettings();
        }
    }

    public Task SaveAsync(AppSettings settings) =>
        _storage.WriteJsonAsync(_storage.GetDataFilePath(FileName), settings);
}

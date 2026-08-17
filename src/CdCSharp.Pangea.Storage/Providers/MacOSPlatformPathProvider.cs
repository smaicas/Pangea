using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Providers;

public class MacOSPlatformPathProvider : IPlatformPathProvider
{
    private readonly IOptions<StorageOptions> _options;

    public MacOSPlatformPathProvider(IOptions<StorageOptions> options)
    {
        _options = options;
    }

    public string GetApplicationDataPath()
    {
        StorageOptions opts = _options.Value;
        if (!string.IsNullOrEmpty(opts.CustomDataPath))
            return opts.CustomDataPath;

        return Path.Combine(ResolveHome(), "Library", "Application Support", opts.ApplicationName);
    }

    public string GetUserDataPath() =>
        Path.Combine(ResolveHome(), "Documents", _options.Value.ApplicationName);

    /// <remarks>
    /// Through the runtime rather than a hard-coded "/tmp", so TMPDIR is honoured - on macOS that
    /// is a per-user directory, which is where temporary files belong.
    /// </remarks>
    public string GetTempPath() =>
        Path.Combine(Path.GetTempPath(), _options.Value.ApplicationName);

    public string GetCachePath() =>
        Path.Combine(ResolveHome(), "Library", "Caches", _options.Value.ApplicationName);

    /// <summary>The user's home, and a fallback that is deliberately not the temp root.</summary>
    private string ResolveHome()
    {
        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home)) return home;

        home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home)) return home;

        return Path.Combine(Path.GetTempPath(), _options.Value.ApplicationName + "-home");
    }
}
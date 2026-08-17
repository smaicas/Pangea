using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Providers;

public class LinuxPlatformPathProvider : IPlatformPathProvider
{
    private readonly IOptions<StorageOptions> _options;

    public LinuxPlatformPathProvider(IOptions<StorageOptions> options)
    {
        _options = options;
    }

    public string GetApplicationDataPath()
    {
        StorageOptions opts = _options.Value;
        if (!string.IsNullOrEmpty(opts.CustomDataPath))
            return opts.CustomDataPath;

        return Path.Combine(ResolveHome(), ".config", opts.ApplicationName);
    }

    public string GetUserDataPath() =>
        Path.Combine(ResolveHome(), _options.Value.ApplicationName);

    public string GetTempPath() =>
        Path.Combine(Path.GetTempPath(), _options.Value.ApplicationName);

    public string GetCachePath() =>
        Path.Combine(ResolveHome(), ".cache", _options.Value.ApplicationName);

    /// <summary>
    /// The user's home, and a fallback that is deliberately not the temp root.
    /// </summary>
    /// <remarks>
    /// Falling back to the temp directory put user data and temporary files in the same place -
    /// this provider keeps user data directly under home - so clearing temporary files would have
    /// taken the user's data with it.
    /// </remarks>
    private string ResolveHome()
    {
        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home)) return home;

        home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home)) return home;

        return Path.Combine(Path.GetTempPath(), _options.Value.ApplicationName + "-home");
    }
}
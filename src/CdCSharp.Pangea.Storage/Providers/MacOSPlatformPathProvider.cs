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

        string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        return Path.Combine(home, "Library", "Application Support", opts.ApplicationName);
    }

    public string GetUserDataPath()
    {
        string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        return Path.Combine(home, "Documents", _options.Value.ApplicationName);
    }

    public string GetTempPath()
    {
        return Path.Combine("/tmp", _options.Value.ApplicationName);
    }

    public string GetCachePath()
    {
        string home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
        return Path.Combine(home, "Library", "Caches", _options.Value.ApplicationName);
    }
}
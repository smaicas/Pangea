using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Providers;

public class WindowsPlatformPathProvider : IPlatformPathProvider
{
    private readonly IOptions<StorageOptions> _options;

    public WindowsPlatformPathProvider(IOptions<StorageOptions> options)
    {
        _options = options;
    }

    public string GetApplicationDataPath()
    {
        StorageOptions opts = _options.Value;
        if (!string.IsNullOrEmpty(opts.CustomDataPath))
            return opts.CustomDataPath;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            opts.ApplicationName);
    }

    public string GetUserDataPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            _options.Value.ApplicationName);
    }

    public string GetTempPath()
    {
        return Path.Combine(Path.GetTempPath(), _options.Value.ApplicationName);
    }

    public string GetCachePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            _options.Value.ApplicationName, "Cache");
    }
}
using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Providers;

public class PortablePlatformPathProvider : IPlatformPathProvider
{
    private readonly IOptions<StorageOptions> _options;
    private readonly string _baseDirectory;

    public PortablePlatformPathProvider(IOptions<StorageOptions> options)
    {
        _options = options;
        _baseDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) 
                         ?? Environment.CurrentDirectory;
    }

    public string GetApplicationDataPath()
    {
        StorageOptions opts = _options.Value;
        return !string.IsNullOrEmpty(opts.CustomDataPath) 
            ? opts.CustomDataPath 
            : Path.Combine(_baseDirectory, "Data");
    }

    public string GetUserDataPath() => Path.Combine(_baseDirectory, "UserData");
    public string GetTempPath() => Path.Combine(_baseDirectory, "Temp");
    public string GetCachePath() => Path.Combine(_baseDirectory, "Cache");
}
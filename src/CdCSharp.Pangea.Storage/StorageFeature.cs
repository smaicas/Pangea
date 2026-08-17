using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Providers;
using CdCSharp.Pangea.Storage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage;

public class StorageFeature : IPangeaFeature
{
    public string Name => "Storage";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StorageOptions>(options => 
        {
            
        });

        services.AddSingleton<IPlatformPathProvider>(serviceProvider =>
        {
            IOptions<StorageOptions> options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
            return CreatePlatformPathProvider(options);
        });
        
        services.AddSingleton<IStorageService, StorageService>();
    }
    
    private static IPlatformPathProvider CreatePlatformPathProvider(IOptions<StorageOptions> options)
    {
        if (options.Value.UsePortableMode)
            return new PortablePlatformPathProvider(options);
        if (OperatingSystem.IsWindows())
            return new WindowsPlatformPathProvider(options);
        if (OperatingSystem.IsLinux())
            return new LinuxPlatformPathProvider(options);
        if (OperatingSystem.IsMacOS())
            return new MacOSPlatformPathProvider(options);

        return new PortablePlatformPathProvider(options);
    }
}
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Core.Abstractions;

public interface IPangeaFeature
{
    string Name { get; }
    Version Version { get; }
    
    void ConfigureServices(IServiceCollection services);
    void ConfigureApplication(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext) { }
}

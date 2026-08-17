using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Binding;

public class BindingFeature : IPangeaFeature
{
    public string Name => "Binding";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        // This feature does not need register specific services.
        // It works in compile time.
    }
}
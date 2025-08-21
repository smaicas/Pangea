using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CdCSharp.Pangea.Core.Configuration;

public class PangeaOptions
{
    public static PangeaOptions Default => new()
    {
        DI = PangeaDIOptions.Default,
        Window = PangeaWindowOptions.Default
    };

    public PangeaDIOptions DI { get; set; } = PangeaDIOptions.Default;
    public PangeaWindowOptions Window { get; set; } = PangeaWindowOptions.Default;
}

public class PangeaDIOptions
{
    public static PangeaDIOptions Default => new()
    {
        AutoRegisterViewModels = true,
        ViewModelLifetime = ServiceLifetime.Transient
    };

    public bool AutoRegisterViewModels { get; set; } = true;
    public List<Assembly> AdditionalAssemblies { get; } = new();
    public ServiceLifetime ViewModelLifetime { get; set; } = ServiceLifetime.Transient;
}

public class PangeaWindowOptions
{
    public static PangeaWindowOptions Default => new()
    {
        AutoDiscoverMainWindow = true
    };

    public Type? MainWindowType { get; set; }
    public Type? MainViewModelType { get; set; }
    public bool AutoDiscoverMainWindow { get; set; } = true;
}
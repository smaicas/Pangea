using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Core.Services;
using CdCSharp.Pangea.Services;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace CdCSharp.Pangea;

public static class PangeaExtensions
{
    public static AppBuilder UsePangea(this AppBuilder builder)
    {
        builder.AfterSetup(appBuilder =>
        {
            ConfigurePangeaServices();
        });

        return builder;
    }

    private static void ConfigurePangeaServices()
    {
        if (Avalonia.Application.Current is not PangeaApplication pangeaApp)
            throw new InvalidOperationException("Application must inherit from PangeaApplication to use Pangea");

        IServiceCollection services = new ServiceCollection();

        // 1 - Register PangeaOptions
        services.Configure<PangeaOptions>((options) => pangeaApp.ConfigurePangeaOptions(options));

        // 2 - Discover and register features
        FeatureRegistry.DiscoverAndRegisterFeatures(services);

        // 3 - Register Core Services
        RegisterCoreServices(services);

        // 4 - Allow App Services Configuration
        pangeaApp.Configure(services);

        // 5 - Discover and register ViewModels
        RegisterViewModels(services);

        // 6 - Build service provider and init static instance
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        pangeaApp.SetValue(PangeaApplication.ServiceProviderProperty, serviceProvider);
        PangeaServices.Initialize(serviceProvider);
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddSingleton<IRelayCommandFactory, RelayCommandFactory>();

        // Nueva configuración de WindowManager con separación de responsabilidades
        services.AddSingleton<IWindowManagerCore, WindowManagerCore>();

        services.AddSingleton<IMainWindowManager>(serviceProvider =>
        {
            IApplicationLifetime applicationLifetime = GetApplicationLifetime();
            IOptions<PangeaOptions> options = serviceProvider.GetRequiredService<IOptions<PangeaOptions>>();
            return new MainWindowManager(serviceProvider, applicationLifetime, options);
        });

        services.AddSingleton<IWindowManager, WindowManager>();
    }

    private static IApplicationLifetime GetApplicationLifetime()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is { } lifetime)
            return lifetime;

        throw new InvalidOperationException("ApplicationLifetime not available during WindowManager creation");
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        using ServiceProvider tempProvider = services.BuildServiceProvider();
        IOptions<PangeaOptions> optionsAccessor = tempProvider.GetRequiredService<IOptions<PangeaOptions>>();
        PangeaOptions options = optionsAccessor.Value;

        if (!options.DI.AutoRegisterViewModels)
            return;

        Type[] viewModelTypes = TypeRegistry.Instance.GetTypesDerivedFrom<ViewModelBase>().ToArray();

        foreach (Type viewModelType in viewModelTypes)
        {
            services.Add(new ServiceDescriptor(viewModelType, viewModelType, options.DI.ViewModelLifetime));
        }
    }
}
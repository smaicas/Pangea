using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Services;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea;

public static class PangeaExtensions
{
    public static AppBuilder UsePangea(this AppBuilder builder)
    {
        builder.AfterSetup(_ => ConfigurePangeaServices());
        return builder;
    }

    private static void ConfigurePangeaServices()
    {
        if (Application.Current is not PangeaApplication pangeaApp)
        {
            throw new InvalidOperationException("Application must inherit from PangeaApplication to use Pangea");
        }

        PangeaOptions options = pangeaApp.ConfigurePangeaOptions(PangeaOptions.Default);

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));

        // One scan of the application's types, shared by everything that needs to look types up.
        TypeRegistry typeRegistry = new(options.DI.AdditionalAssemblies);
        typeRegistry.Initialize();
        services.AddSingleton(typeRegistry);

        FeatureRegistry featureRegistry = new(typeRegistry);
        featureRegistry.DiscoverAndRegister(services);
        services.AddSingleton(featureRegistry);

        RegisterCoreServices(services);

        pangeaApp.Configure(services);

        RegisterViewModels(services, typeRegistry, options);

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        pangeaApp.SetValue(PangeaApplication.ServiceProviderProperty, serviceProvider);
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        // No providers by default: the toolkit logs, the application decides where that goes.
        services.AddLogging();

        // The dispatcher comes first: the command factory hands it to every command it builds.
        services.AddSingleton<IUIDispatcher, AvaloniaUIDispatcher>();
        services.AddSingleton<IRelayCommandFactory, RelayCommandFactory>();
        services.AddSingleton(GetApplicationLifetime());
        services.AddSingleton<IWindowManager, WindowManager>();
    }

    private static IApplicationLifetime GetApplicationLifetime() =>
        Application.Current?.ApplicationLifetime
        ?? throw new InvalidOperationException("ApplicationLifetime not available during Pangea startup");

    private static void RegisterViewModels(IServiceCollection services, TypeRegistry typeRegistry, PangeaOptions options)
    {
        if (!options.DI.AutoRegisterViewModels) return;

        foreach (Type viewModelType in typeRegistry.GetTypesDerivedFrom<ViewModelBase>())
        {
            services.Add(new ServiceDescriptor(viewModelType, viewModelType, options.DI.ViewModelLifetime));
        }
    }
}

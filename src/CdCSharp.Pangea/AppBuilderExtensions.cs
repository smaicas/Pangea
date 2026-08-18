using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Dialogs;
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
        builder.AfterSetup(_ => ConfigureServices(Application.Current));
        return builder;
    }

    /// <summary>
    /// Builds the container for <paramref name="application"/> and publishes it on the application.
    /// </summary>
    /// <remarks>
    /// Takes the application rather than reading <see cref="Application.Current"/> for itself, so
    /// every step composes the same instance and the whole of startup can be exercised by a test.
    /// </remarks>
    internal static IServiceProvider ConfigureServices(Application? application)
    {
        if (application is not PangeaApplication pangeaApp)
        {
            throw new InvalidOperationException("Application must inherit from PangeaApplication to use Pangea");
        }

        PangeaOptions options = pangeaApp.ConfigurePangeaOptions(PangeaOptions.Default);

        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));

        // What the compiler worked out, if the generator ran over the application itself.
        // Everything below prefers it and falls back to reading the assemblies when it is empty.
        PangeaCatalogIndex catalog = new(PangeaCatalogs.All);

        if (!catalog.Covers(pangeaApp.GetType().Assembly)) catalog = PangeaCatalogIndex.Empty;

        services.AddSingleton(catalog);

        // One scan of the application's types, shared by everything that needs to look types up.
        TypeRegistry typeRegistry = new(options.DI.AdditionalAssemblies);
        services.AddSingleton(typeRegistry);

        // Paid for up front only when it is going to be needed: with a catalog in hand, nothing
        // may ever ask the registry a question, and this is the slowest thing startup does.
        if (catalog.IsEmpty) typeRegistry.Initialize();

        FeatureRegistry featureRegistry = new(typeRegistry, catalog);
        featureRegistry.DiscoverAndRegister(services);
        services.AddSingleton(featureRegistry);

        RegisterCoreServices(services, pangeaApp);

        pangeaApp.Configure(services);

        RegisterViewModels(services, typeRegistry, catalog, options);

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        pangeaApp.SetValue(PangeaApplication.ServiceProviderProperty, serviceProvider);
        return serviceProvider;
    }

    private static void RegisterCoreServices(IServiceCollection services, Application application)
    {
        // No providers by default: the toolkit logs, the application decides where that goes.
        services.AddLogging();

        // The dispatcher comes first: the command factory hands it to every command it builds.
        services.AddSingleton<IUIDispatcher, AvaloniaUIDispatcher>();
        services.AddSingleton<IRelayCommandFactory, RelayCommandFactory>();
        services.AddSingleton(_ => GetApplicationLifetime(application));
        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<IDialogService, DialogService>();
    }

    /// <summary>
    /// Read when something asks for it rather than while the container is being built.
    /// </summary>
    /// <remarks>
    /// An application hosted without a lifetime - a headless test session, a XAML designer - has
    /// no windows to manage, but everything else it is made of still works. Capturing the lifetime
    /// here would fail all of it for the sake of the part that was never going to run.
    /// </remarks>
    private static IApplicationLifetime GetApplicationLifetime(Application application) =>
        application.ApplicationLifetime
        ?? throw new InvalidOperationException("ApplicationLifetime not available during Pangea startup");

    /// <summary>
    /// Registers every view model, with the generated factory when there is one.
    /// </summary>
    /// <remarks>
    /// A descriptor naming only the type leaves the container to find a constructor and call it by
    /// reflection. A descriptor carrying a factory does not: the generated one is a plain
    /// <c>new</c>, so the constructor is referenced by code the trimmer can see and nothing is
    /// resolved by name at runtime.
    /// </remarks>
    private static void RegisterViewModels(
        IServiceCollection services, TypeRegistry typeRegistry, PangeaCatalogIndex catalog, PangeaOptions options)
    {
        if (!options.DI.AutoRegisterViewModels) return;

        HashSet<Type> registered = [];

        if (!catalog.IsEmpty)
        {
            foreach (PangeaViewModelEntry entry in catalog.ViewModels)
            {
                if (registered.Add(entry.ViewModelType))
                {
                    services.Add(new ServiceDescriptor(entry.ViewModelType, entry.Create, options.DI.ViewModelLifetime));
                }
            }

            // Assemblies named by hand are not compiled with this application, so nothing generated
            // describes them and they are still found the old way.
            if (options.DI.AdditionalAssemblies.Count == 0) return;
        }

        foreach (Type viewModelType in typeRegistry.GetTypesDerivedFrom<ViewModelBase>())
        {
            if (registered.Add(viewModelType))
            {
                services.Add(new ServiceDescriptor(viewModelType, viewModelType, options.DI.ViewModelLifetime));
            }
        }
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Security;
using CdCSharp.Pangea.Services;
using CdCSharp.Pangea.Shell;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea;

public static class PangeaExtensions
{
    /// <summary>
    /// Builds the container, discovers the features and registers the view models.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="platformServices"/> is where a platform head registers what only it can
    /// build - the Android Keystore, an iOS connectivity monitor - and it exists because there is
    /// nowhere else for those to go. A head cannot use the application's own
    /// <see cref="PangeaApplication.Configure"/>: that lives in the shared library, which by
    /// definition cannot see Android. A feature in the head would work only if the head is scanned
    /// or generates a catalog. This runs where the head already is, which is here: the head is what
    /// builds the <see cref="AppBuilder"/>.
    /// </para>
    /// <para>
    /// It runs before anything else registers, and the toolkit's own services are registered with
    /// <c>TryAdd</c>, so what the platform provides is what the application resolves and the
    /// defaults fill in the rest. The application's <c>Configure</c> still runs last and still has
    /// the final word.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
    ///     base.CustomizeAppBuilder(builder)
    ///         .UsePangea(services =>
    ///         {
    ///             services.AddSingleton&lt;ISecretStore&gt;(_ => new KeystoreSecretStore(this));
    ///             services.AddSingleton&lt;IConnectivity&gt;(_ => new AndroidConnectivity(this));
    ///         });
    /// </code>
    /// </example>
    public static AppBuilder UsePangea(this AppBuilder builder, Action<IServiceCollection>? platformServices = null)
    {
        builder.AfterSetup(_ => ConfigureServices(Application.Current, platformServices));
        return builder;
    }

    /// <summary>
    /// Builds the container for <paramref name="application"/> and publishes it on the application.
    /// </summary>
    /// <remarks>
    /// Takes the application rather than reading <see cref="Application.Current"/> for itself, so
    /// every step composes the same instance and the whole of startup can be exercised by a test.
    /// </remarks>
    internal static IServiceProvider ConfigureServices(
        Application? application, Action<IServiceCollection>? platformServices = null)
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

        // First, so that everything after it fills in what the platform did not provide rather than
        // overwriting what it did.
        platformServices?.Invoke(services);

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

    /// <summary>
    /// The services every Pangea application has, unless something already provided them.
    /// </summary>
    /// <remarks>
    /// <c>TryAdd</c> throughout, which is what makes the platform hook and the features able to
    /// replace any of these: a default is only a default if something else can win. The
    /// application's own <c>Configure</c> runs after this and registers outright, so it wins by
    /// being last rather than by being first.
    /// </remarks>
    private static void RegisterCoreServices(IServiceCollection services, Application application)
    {
        // No providers by default: the toolkit logs, the application decides where that goes.
        services.AddLogging();

        // The dispatcher comes first: the command factory hands it to every command it builds.
        services.TryAddSingleton<IUIDispatcher, AvaloniaUIDispatcher>();
        services.TryAddSingleton<IRelayCommandFactory, RelayCommandFactory>();
        services.TryAddSingleton(_ => GetApplicationLifetime(application));
        services.TryAddSingleton<IWindowManager, WindowManager>();
        services.TryAddSingleton<IDialogService, DialogService>();
        services.TryAddSingleton(BuildShellPresenter);

        // An answer everywhere, and a better answer on the platforms that have one of their own.
        services.TryAddSingleton<IConnectivity, NetworkConnectivity>();
        services.TryAddSingleton<ISecretStore, FileSecretStore>();
    }

    /// <summary>
    /// Which shell this platform has, decided once.
    /// </summary>
    /// <remarks>
    /// Resolved lazily for the reason the lifetime is: an application hosted without one - a
    /// headless test session, a XAML designer - has no shell to present anything in, and deciding
    /// here would fail everything else it is made of for the sake of the part that was never going
    /// to run.
    /// </remarks>
    private static IShellPresenter BuildShellPresenter(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IApplicationLifetime>() switch
        {
            ISingleViewApplicationLifetime singleView => new SingleViewShellPresenter(
                serviceProvider,
                new LifetimeSingleViewSurface(singleView),
                serviceProvider.GetRequiredService<IOptions<PangeaOptions>>(),
                serviceProvider.GetRequiredService<TypeRegistry>(),
                serviceProvider.GetRequiredService<ILogger<SingleViewShellPresenter>>(),
                serviceProvider.GetService<PangeaCatalogIndex>()),

            // Desktop is the fallback rather than a match on IClassicDesktopStyleApplicationLifetime:
            // a lifetime the toolkit has never heard of still has windows more often than not, and
            // the window manager says so plainly if it turns out not to.
            _ => new DesktopShellPresenter(serviceProvider, serviceProvider.GetRequiredService<IWindowManager>())
        };

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

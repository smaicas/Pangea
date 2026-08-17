using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Services;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// The path every Pangea application takes on startup: one type scan, the features, the core
/// services, the application's own registrations, and the view models.
/// </summary>
public class BootstrapTests
{
    private sealed class StubApplication : PangeaApplication;

    private sealed class OptionsApplication : PangeaApplication
    {
        public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
        {
            // Deliberately a different instance from the one handed in: mutating the argument would
            // pass even if startup threw the return value away, which is the failure being guarded.
            PangeaOptions replacement = PangeaOptions.Default;
            replacement.DI.AutoRegisterViewModels = false;
            return replacement;
        }
    }

    private sealed class ConfiguringApplication : PangeaApplication
    {
        public override void Configure(IServiceCollection services) =>
            services.AddSingleton<IMarker, Marker>();
    }

    private interface IMarker;

    private sealed class Marker : IMarker;

    public sealed class SampleViewModel(IServiceProvider services) : ViewModelBase(services);

    private static IServiceProvider Bootstrap(PangeaApplication application)
    {
        application.ApplicationLifetime = new ClassicDesktopStyleApplicationLifetime();
        return PangeaExtensions.ConfigureServices(application);
    }

    [AvaloniaFact]
    public void AnApplicationThatIsNotAPangeaApplication_IsRejectedWithAReason()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => PangeaExtensions.ConfigureServices(new Application()));

        Assert.Contains("PangeaApplication", error.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void NoApplicationAtAll_IsRejectedRatherThanNullReferenced()
    {
        Assert.Throws<InvalidOperationException>(() => PangeaExtensions.ConfigureServices(null));
    }

    [AvaloniaFact]
    public void WithoutAnApplicationLifetime_StartupSaysSo()
    {
        StubApplication application = new();

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => PangeaExtensions.ConfigureServices(application));

        Assert.Contains("ApplicationLifetime", error.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void EveryCoreServiceIsResolvable()
    {
        IServiceProvider services = Bootstrap(new StubApplication());

        Assert.NotNull(services.GetService<IUIDispatcher>());
        Assert.NotNull(services.GetService<IRelayCommandFactory>());
        Assert.NotNull(services.GetService<IWindowManager>());
        Assert.NotNull(services.GetService<TypeRegistry>());
        Assert.NotNull(services.GetService<FeatureRegistry>());
        Assert.NotNull(services.GetService<IApplicationLifetime>());
        Assert.NotNull(services.GetService<IOptions<PangeaOptions>>());
    }

    [AvaloniaFact]
    public void TheContainerIsPublishedOnTheApplication()
    {
        StubApplication application = new();

        IServiceProvider services = Bootstrap(application);

        Assert.Same(services, application.GetServiceProvider());
    }

    /// <summary>
    /// The whole point of one scan: every consumer looks types up through the same registry.
    /// </summary>
    [AvaloniaFact]
    public void TheTypeRegistryIsASingleSharedInstance()
    {
        IServiceProvider services = Bootstrap(new StubApplication());

        Assert.Same(services.GetService<TypeRegistry>(), services.GetService<TypeRegistry>());
    }

    [AvaloniaFact]
    public void ViewModelsAreRegisteredAutomatically()
    {
        IServiceProvider services = Bootstrap(new StubApplication());

        Assert.NotNull(services.GetService<SampleViewModel>());
    }

    /// <summary>
    /// The options the application returns are the options startup uses. Returning a fresh instance
    /// and having it ignored would silently disable everything configured on it.
    /// </summary>
    [AvaloniaFact]
    public void TheOptionsReturnedByTheApplicationAreTheOnesInEffect()
    {
        IServiceProvider services = Bootstrap(new OptionsApplication());

        Assert.False(services.GetRequiredService<IOptions<PangeaOptions>>().Value.DI.AutoRegisterViewModels);
        Assert.Null(services.GetService<SampleViewModel>());
    }

    [AvaloniaFact]
    public void TheApplicationsOwnRegistrationsSurvive()
    {
        IServiceProvider services = Bootstrap(new ConfiguringApplication());

        Assert.IsType<Marker>(services.GetService<IMarker>());
    }

    [AvaloniaFact]
    public void TheDispatcherIsWiredToTheUIThread()
    {
        IServiceProvider services = Bootstrap(new StubApplication());
        IUIDispatcher dispatcher = services.GetRequiredService<IUIDispatcher>();

        // The test body owns the UI thread.
        Assert.True(dispatcher.CheckAccess());
        Assert.False(Task.Run(dispatcher.CheckAccess).GetAwaiter().GetResult());
    }
}

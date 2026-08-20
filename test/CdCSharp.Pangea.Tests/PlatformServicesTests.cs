using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Where a platform head registers what only it can build.
/// </summary>
/// <remarks>
/// The head has nowhere else to put these. The application's own <c>Configure</c> lives in the
/// shared library, which cannot see Android; a feature in the head is found only if the head is
/// scanned or carries a generated catalog. What the head always has is the <c>AppBuilder</c> it
/// builds, so that is where the hook is.
/// </remarks>
public class PlatformServicesTests
{
    private interface IProbe
    {
        string Name { get; }
    }

    private sealed class PlatformProbe : IProbe
    {
        public string Name => "platform";
    }

    private sealed class ApplicationProbe : IProbe
    {
        public string Name => "application";
    }

    private sealed class PlatformConnectivity : IConnectivity
    {
        public bool IsConnected => true;

        public event EventHandler<ConnectivityChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }
    }

    private sealed class PlatformSecretStore : ISecretStore
    {
        public SecretProtection Protection => SecretProtection.Device;

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PlainApplication : PangeaApplication;

    private sealed class ConfiguringApplication : PangeaApplication
    {
        public override void Configure(IServiceCollection services) =>
            services.AddSingleton<IProbe, ApplicationProbe>();
    }

    private static IServiceProvider Bootstrap(
        PangeaApplication application, Action<IServiceCollection>? platformServices)
    {
        application.ApplicationLifetime = new ClassicDesktopStyleApplicationLifetime();
        return PangeaExtensions.ConfigureServices(application, platformServices);
    }

    /// <summary>
    /// The reason the hook exists: a head replacing a service the toolkit registers a default for.
    /// Before this, the toolkit's own registration ran last and won, and there was no way round it
    /// from a platform head at all.
    /// </summary>
    [AvaloniaFact]
    public void WhatThePlatformRegisters_BeatsTheToolkitsDefault()
    {
        IServiceProvider services = Bootstrap(new PlainApplication(), platform =>
        {
            platform.AddSingleton<ISecretStore, PlatformSecretStore>();
            platform.AddSingleton<IConnectivity, PlatformConnectivity>();
        });

        Assert.IsType<PlatformSecretStore>(services.GetRequiredService<ISecretStore>());
        Assert.IsType<PlatformConnectivity>(services.GetRequiredService<IConnectivity>());
        Assert.Equal(SecretProtection.Device, services.GetRequiredService<ISecretStore>().Protection);
    }

    [AvaloniaFact]
    public void WithNoPlatformServices_TheDefaultsAreStillThere()
    {
        IServiceProvider services = Bootstrap(new PlainApplication(), platformServices: null);

        Assert.NotNull(services.GetRequiredService<ISecretStore>());
        Assert.NotNull(services.GetRequiredService<IConnectivity>());
        Assert.NotNull(services.GetRequiredService<IUIDispatcher>());
    }

    /// <summary>
    /// The application still has the last word. It runs after the head and registers outright, so
    /// an application that means to override its own platform can.
    /// </summary>
    [AvaloniaFact]
    public void TheApplicationsOwnConfigure_StillWins()
    {
        IServiceProvider services = Bootstrap(
            new ConfiguringApplication(), platform => platform.AddSingleton<IProbe, PlatformProbe>());

        Assert.Equal("application", services.GetRequiredService<IProbe>().Name);
    }

    /// <summary>
    /// Registered before the features run, so a feature's <c>TryAdd</c> leaves it alone - and so a
    /// feature can depend on something only the platform can build.
    /// </summary>
    [AvaloniaFact]
    public void ThePlatformRunsBeforeTheFeatures()
    {
        List<string> order = [];

        IServiceProvider services = Bootstrap(new PlainApplication(), platform =>
        {
            order.Add("platform");
            platform.AddSingleton<IProbe, PlatformProbe>();
        });

        Assert.Equal(["platform"], order);

        // Nothing after it replaced what it registered.
        Assert.Equal("platform", services.GetRequiredService<IProbe>().Name);
    }

    /// <summary>
    /// Every core service is a default rather than a decree: a head or a feature that registers one
    /// first keeps it.
    /// </summary>
    [AvaloniaFact]
    public void TheCoreServicesAreDefaults_NotDecrees()
    {
        CountingDispatcher dispatcher = new();

        IServiceProvider services = Bootstrap(
            new PlainApplication(), platform => platform.AddSingleton<IUIDispatcher>(dispatcher));

        Assert.Same(dispatcher, services.GetRequiredService<IUIDispatcher>());
    }

    private sealed class CountingDispatcher : IUIDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public void Invoke(Action action) => action();

        public T Invoke<T>(Func<T> callback) => callback();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }
}

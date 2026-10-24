using Avalonia.Styling;
using CdCSharp.Pangea.Apps.Testing;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Testing.Dispatchers;
using CdCSharp.Pangea.Testing.Fakes;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>
/// The host imposes its look while it is connected; losing it gives the application back exactly
/// what it had.
/// </summary>
public class HostEnvironmentTests
{
    [Fact]
    public void The_host_theme_is_worn_while_connected_and_the_own_one_comes_back_after()
    {
        FakeAppHost host = new();
        RecordingThemeService themes = new();
        themes.SetVariant(ThemeVariant.Dark);

        HostEnvironmentApplier applier = Build(host, themes);

        host.Connect("Omni", new AppEnvironment { Theme = Snapshot(AppThemeVariant.Light) });
        applier.Update();

        Assert.Equal(HostEnvironmentApplier.HostThemeName, themes.CurrentTheme);
        Assert.Equal(ThemeVariant.Light, themes.CurrentVariant);

        host.Disconnect();
        applier.Update();

        Assert.Equal(PangeaTheme.DefaultName, themes.CurrentTheme);
        Assert.Equal(ThemeVariant.Dark, themes.CurrentVariant);
    }

    [Fact]
    public void The_same_theme_sent_again_is_not_rebuilt()
    {
        FakeAppHost host = new();
        RecordingThemeService themes = new();
        HostEnvironmentApplier applier = Build(host, themes);

        host.Connect("Omni", new AppEnvironment { Theme = Snapshot(AppThemeVariant.Dark) });
        applier.Update();
        host.Connect("Omni", new AppEnvironment { Theme = Snapshot(AppThemeVariant.Dark) });
        applier.Update();

        Assert.Single(themes.ThemesSet);
    }

    [Fact]
    public void An_application_that_opts_out_keeps_its_own_theme()
    {
        FakeAppHost host = new();
        RecordingThemeService themes = new();
        HostEnvironmentApplier applier = Build(host, themes, options => options.FollowHostTheme = false);

        host.Connect("Omni", new AppEnvironment { Theme = Snapshot(AppThemeVariant.Light) });
        applier.Update();

        Assert.Empty(themes.ThemesSet);
    }

    private static HostEnvironmentApplier Build(FakeAppHost host, IThemeService themes, Action<AppsOptions>? configure = null)
    {
        AppsOptions options = new();
        configure?.Invoke(options);

        ServiceProvider services = new ServiceCollection().AddSingleton(themes).BuildServiceProvider();

        return new HostEnvironmentApplier(host, services, new InlineUIDispatcher(), Options.Create(options), NullLogger<HostEnvironmentApplier>.Instance);
    }

    private static AppThemeSnapshot Snapshot(AppThemeVariant variant) => new(
        new LightPalette().ToColours().ToDictionary(pair => pair.Key, pair => pair.Value.ToString()),
        new DarkPalette().ToColours().ToDictionary(pair => pair.Key, pair => pair.Value.ToString()))
    {
        Variant = variant
    };
}

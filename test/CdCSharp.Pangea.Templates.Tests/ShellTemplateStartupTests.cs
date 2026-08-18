using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PangeaShellApp.Services;
using PangeaShellApp.ViewModels;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// What a generated project does the moment it is run.
/// </summary>
/// <remarks>
/// Compiling proves the template's code is valid; none of this is visible until it starts. Every
/// failure these catch - an unregistered service, a view the locator cannot name, a request that
/// reaches nothing - happens at startup or on the first navigation, so a broken template would
/// otherwise reach whoever generated it intact.
/// </remarks>
public class ShellTemplateStartupTests
{
    private static IServiceProvider Services =>
        ((PangeaApplication)Application.Current!).GetServiceProvider();

    /// <summary>
    /// Startup read the generated catalog rather than the assemblies.
    /// </summary>
    /// <remarks>
    /// The two paths answer the same questions, so a regression back to scanning would pass every
    /// other test in this file. This is the one that would notice.
    /// </remarks>
    [AvaloniaFact]
    public void Startup_ReadsTheGeneratedCatalog()
    {
        PangeaCatalogIndex catalog = Services.GetRequiredService<PangeaCatalogIndex>();

        Assert.False(catalog.IsEmpty);
        Assert.True(catalog.Covers(typeof(PangeaShellApp.App).Assembly));

        Assert.Contains(catalog.ViewModels, entry => entry.ViewModelType == typeof(HomeViewModel));
        Assert.Contains(catalog.Features, build => build() is PangeaShellApp.Services.AppStartupFeature);
        Assert.Contains(catalog.NavigationRequests,
            entry => entry.RequestType == typeof(PangeaShellApp.Navigation.ShowOrderDetail));

        // Two applications live in this assembly, so the shell's view has to be the one found for
        // the shell's view model rather than whichever was catalogued first.
        Assert.Equal(
            typeof(PangeaShellApp.Views.MainWindow),
            catalog.FindView("MainWindow", typeof(MainWindowViewModel).Namespace)?.ViewType);
    }

    [AvaloniaFact]
    public void TheShellWindow_IsTheOneStartupWouldDiscover()
    {
        // What the window manager looks for by name at startup, and what the locator would pair
        // with it. A headless session has no lifetime to show a window in, so the pairing is
        // checked rather than the showing.
        Window shell = Assert.IsType<PangeaShellApp.Views.MainWindow>(
            Services.GetRequiredService<IViewLocator>().Locate(Services.GetRequiredService<MainWindowViewModel>()));

        Assert.IsType<MainWindowViewModel>(shell.DataContext);
    }

    [AvaloniaFact]
    public void EveryServiceTheTemplateAsksFor_IsRegistered()
    {
        // Each of these is resolved by a view model's constructor, which is the moment a missing
        // registration becomes a crash on screen.
        Assert.NotNull(Services.GetRequiredService<LocalizedStrings>());
        Assert.NotNull(Services.GetRequiredService<AppSettingsStore>());
        Assert.NotNull(Services.GetRequiredService<ILocalizationService>());
        Assert.NotNull(Services.GetRequiredService<IThemeService>());
        Assert.NotNull(Services.GetRequiredService<IStorageService>());
        Assert.NotNull(Services.GetRequiredService<INavigationService>());
    }

    [AvaloniaFact]
    public void TheShell_OpensOnHome()
    {
        // Building the shell is what startup does; it navigates from its own constructor.
        _ = Services.GetRequiredService<MainWindowViewModel>();

        Assert.IsType<HomeViewModel>(Services.GetRequiredService<INavigationService>().CurrentViewModel);
    }

    [AvaloniaFact]
    public async Task EveryScreen_HasAViewTheLocatorCanFind()
    {
        INavigationService navigation = Services.GetRequiredService<INavigationService>();
        IViewLocator locator = Services.GetRequiredService<IViewLocator>();

        await navigation.NavigateToAsync<HomeViewModel>();
        await navigation.NavigateToAsync<SettingsViewModel>();

        Assert.IsType<PangeaShellApp.Views.HomeView>(
            locator.Locate(Services.GetRequiredService<HomeViewModel>()));
        Assert.IsType<PangeaShellApp.Views.SettingsView>(
            locator.Locate(Services.GetRequiredService<SettingsViewModel>()));
        Assert.IsType<PangeaShellApp.Views.OrderDetailView>(
            locator.Locate(Services.GetRequiredService<OrderDetailViewModel>()));

        Assert.IsType<SettingsViewModel>(navigation.CurrentViewModel);
    }

    [AvaloniaFact]
    public async Task ATypedRequest_ArrivesAtTheScreenItNames()
    {
        INavigationService navigation = Services.GetRequiredService<INavigationService>();

        await navigation.NavigateToAsync(new PangeaShellApp.Navigation.ShowOrderDetail("ORD-0007", "Ada Lovelace"));

        OrderDetailViewModel screen = Assert.IsType<OrderDetailViewModel>(navigation.CurrentViewModel);
        Assert.Equal("ORD-0007", screen.Reference);
        Assert.Equal("Ada Lovelace", screen.Customer);
    }

    [AvaloniaFact]
    public void TheStringsTheViewsBindTo_AreAllTranslated()
    {
        ILocalizationService localization = Services.GetRequiredService<ILocalizationService>();
        LocalizedStrings strings = Services.GetRequiredService<LocalizedStrings>();

        // GetString returns the key when it resolves to nothing, so a key that comes back
        // unchanged is a string the views would show raw.
        foreach (string key in ShellResourceKeys)
        {
            localization.SetCulture("en-US");
            Assert.NotEqual(key, strings[key]);

            localization.SetCulture("es-ES");
            Assert.NotEqual(key, strings[key]);
        }

        localization.SetCulture("en-US");
    }

    /// <summary>Every key named by the shell template's XAML and view models.</summary>
    private static readonly string[] ShellResourceKeys =
    [
        "Nav_Home", "Nav_Settings", "Nav_Back",
        "Home_Title", "Home_Subtitle", "Home_Customer", "Home_Add", "Home_Open", "Home_Empty",
        "Order_Title", "Order_Customer", "Order_Reference",
        "Settings_Title", "Settings_Language", "Settings_Appearance", "Settings_Save",
        "Settings_Saved", "Settings_Unsaved",
        "Settings_Discard_Title", "Settings_Discard_Message",
        "Settings_Discard_Confirm", "Settings_Discard_Cancel"
    ];
}

using Avalonia.Styling;
using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Localization.Controls;
using CdCSharp.Pangea.Testing;
using CdCSharp.Pangea.Testing.Fakes;
using PangeaShellApp.Navigation;
using PangeaShellApp.Services;
using PangeaShellApp.ViewModels;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// The shell template's view models, driven with no application at all.
/// </summary>
/// <remarks>
/// Ordinary facts rather than Avalonia ones: nothing here needs a dispatcher, a window or a
/// container. This is what <c>CdCSharp.Pangea.Testing</c> is for, and running the template's own
/// screens through it is how the package is checked against something real.
/// </remarks>
public class ShellViewModelTests
{
    private static PangeaTestServices Services()
    {
        PangeaTestServices services = new();

        DictionaryLocalizationService localization = new(
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en-US"] = new Dictionary<string, string>
                {
                    ["Settings_Discard_Title"] = "Unsaved changes",
                    ["Settings_Discard_Message"] = "Leave this screen and discard the changes?",
                    ["Settings_Discard_Confirm"] = "Discard",
                    ["Settings_Discard_Cancel"] = "Stay",
                    ["Settings_Saved"] = "Saved."
                },
                ["es-ES"] = new Dictionary<string, string> { ["Settings_Saved"] = "Guardado." }
            });

        services.Add<ILocalizationService>(localization);
        services.Add(new LocalizedStrings(localization));
        services.Add(new AppSettingsStore(services.Storage));

        // The settings screen asks the container for the toolkit's picker, which the container
        // would have registered for itself in a real application.
        services.Add(new LanguageSelectorViewModel(services));

        return services;
    }

    [Fact]
    public void AddingAnOrder_ClearsTheFormAndShowsTheNewRow()
    {
        PangeaTestServices services = Services();
        HomeViewModel home = new(services) { NewCustomer = "Ada Lovelace" };

        home.AddOrderCommand.Execute(null);

        Assert.Contains(home.Orders, order => order.Customer == "Ada Lovelace");
        Assert.Equal("", home.NewCustomer);
    }

    /// <summary>
    /// The rules are declared on the field and enforced by the generated setter, so the command
    /// gate and the validation state agree without the view being involved.
    /// </summary>
    [Fact]
    public void AddingAnOrderWithAnInvalidCustomer_DoesNothingAndSaysWhy()
    {
        PangeaTestServices services = Services();
        HomeViewModel home = new(services);

        int before = home.Orders.Count;
        home.NewCustomer = "A";

        Assert.True(home.HasErrors);
        Assert.False(home.CanAddOrder);

        home.AddOrderCommand.Execute(null);

        Assert.Equal(before, home.Orders.Count);
    }

    [Fact]
    public void OpeningAnOrder_NavigatesWithTheOrderItWasGiven()
    {
        PangeaTestServices services = Services();
        HomeViewModel home = new(services);

        home.OpenOrderCommand.Execute(home.Orders[0]);

        Assert.Equal(typeof(OrderDetailViewModel), services.Navigation.LastDestination);
        Assert.Equal(home.Orders[0].Reference, services.Navigation.LastRequest<ShowOrderDetail>()?.Reference);
    }

    [Fact]
    public async Task SavingSettings_AppliesThemAndWritesThemDown()
    {
        PangeaTestServices services = Services();
        SettingsViewModel settings = new(services) { IsDark = true };

        // Picking a language applies it there and then; Save only records what is already applied.
        settings.LanguageSelector.SelectedLanguage =
            settings.LanguageSelector.AvailableLanguages.Single(language => language.Name == "es-ES");

        Assert.Equal("es-ES", services.GetRequiredLocalization().CurrentCulture.Name);
        Assert.True(settings.HasUnsavedChanges);

        settings.SaveCommand.Execute(null);

        Assert.Equal("es-ES", services.GetRequiredLocalization().CurrentCulture.Name);
        Assert.Equal(ThemeVariant.Dark, services.Theming.CurrentVariant);
        Assert.False(settings.HasUnsavedChanges);

        AppSettings saved = await new AppSettingsStore(services.Storage).LoadAsync();

        Assert.Equal("es-ES", saved.Culture);
        Assert.True(saved.IsDark);
    }

    /// <summary>
    /// The screen refuses to be left with unsaved changes, and the wording it asks with is a
    /// localized string worth checking.
    /// </summary>
    [Fact]
    public async Task LeavingSettingsWithUnsavedChanges_AsksFirstAndObeysTheAnswer()
    {
        PangeaTestServices services = Services();
        services.Dialogs.Answering(false, true);

        SettingsViewModel settings = new(services) { IsDark = true };

        Assert.False(await settings.CanNavigateAwayAsync());
        Assert.True(await settings.CanNavigateAwayAsync());

        Assert.Equal("Unsaved changes", services.Dialogs.Confirmations[0].Title);
        Assert.Equal("Stay", services.Dialogs.Confirmations[0].CancelText);
    }

    [Fact]
    public async Task LeavingSettingsWithNothingToLose_AsksNothing()
    {
        PangeaTestServices services = Services();
        SettingsViewModel settings = new(services);

        Assert.True(await settings.CanNavigateAwayAsync());
        Assert.Empty(services.Dialogs.Confirmations);
    }
}

internal static class TestServiceExtensions
{
    public static ILocalizationService GetRequiredLocalization(this PangeaTestServices services) =>
        (ILocalizationService)services.GetService(typeof(ILocalizationService))!;
}

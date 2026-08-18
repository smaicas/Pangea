using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea.Localization.Controls;
using CdCSharp.Pangea.Localization.Tests.Infrastructure;

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// Picking a language, and the control that offers the choice.
/// </summary>
/// <remarks>
/// The picker applies the language as soon as one is chosen: there is no confirming step between
/// choosing a language and reading the window in it.
/// </remarks>
public class LanguageSelectorTests
{
    private static LanguageSelectorViewModel Build(out StubLocalizationService localization,
        params string[] supported)
    {
        localization = new StubLocalizationService(supported.Length == 0 ? ["en-US", "es-ES"] : supported);
        return new LanguageSelectorViewModel(new TestServices(localization));
    }

    [Fact]
    public void ItOffersTheSupportedCultures_AndStartsOnTheCurrentOne()
    {
        LanguageSelectorViewModel selector = Build(out StubLocalizationService localization);

        Assert.Equal(["en-US", "es-ES"], selector.AvailableLanguages.Select(language => language.Name));
        Assert.Equal(localization.CurrentCulture.Name, selector.SelectedLanguage?.Name);
    }

    /// <summary>
    /// Someone looking for Spanish in a window currently in English is looking for "Español". That
    /// is the one label in a language picker that must never be localized.
    /// </summary>
    [Fact]
    public void EachLanguageIsNamedInItself()
    {
        LanguageSelectorViewModel selector = Build(out _);

        LanguageOption spanish = selector.AvailableLanguages.Single(language => language.Name == "es-ES");

        Assert.StartsWith("E", spanish.DisplayName, StringComparison.Ordinal);
        Assert.Contains("spañol", spanish.DisplayName, StringComparison.Ordinal);
        Assert.Equal(spanish.DisplayName, spanish.ToString());
    }

    [Fact]
    public void PickingALanguage_AppliesItImmediately()
    {
        LanguageSelectorViewModel selector = Build(out StubLocalizationService localization);

        selector.SelectedLanguage = selector.AvailableLanguages.Single(language => language.Name == "es-ES");

        Assert.Equal("es-ES", localization.CurrentCulture.Name);
    }

    /// <summary>
    /// A picker showing a language the application is not in is worse than no picker.
    /// </summary>
    [Fact]
    public void WhenApplyingFails_ThePickerRollsBack()
    {
        StubLocalizationService localization = new(["en-US", "es-ES"]);
        LanguageSelectorViewModel selector = new(new TestServices(localization));

        // A culture the service will refuse, reached the way a stale saved setting would reach it.
        LanguageOption unsupported = new(System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
        selector.AvailableLanguages.Add(unsupported);

        selector.SelectedLanguage = unsupported;

        Assert.Equal("en-US", localization.CurrentCulture.Name);
        Assert.Equal("en-US", selector.SelectedLanguage?.Name);
    }

    /// <summary>
    /// The culture is not the picker's to own: a settings file restored at startup changes it too.
    /// </summary>
    [Fact]
    public void AChangeMadeElsewhere_MovesThePicker()
    {
        LanguageSelectorViewModel selector = Build(out StubLocalizationService localization);

        localization.SetCulture("es-ES");

        Assert.Equal("es-ES", selector.SelectedLanguage?.Name);
    }

    [Fact]
    public void DisposingStopsFollowingTheService()
    {
        LanguageSelectorViewModel selector = Build(out StubLocalizationService localization);
        selector.Dispose();

        localization.SetCulture("es-ES");

        Assert.Equal("en-US", selector.SelectedLanguage?.Name);
    }

    /// <summary>Stands in for the window's view model, which is what the binding reads.</summary>
    private sealed class Host
    {
        public Host(LanguageSelectorViewModel selector) => LanguageSelector = selector;

        public LanguageSelectorViewModel LanguageSelector { get; }
    }

    /// <summary>
    /// The documented usage, exercised the way a window does it.
    /// </summary>
    /// <remarks>
    /// <c>ViewModel="{Binding LanguageSelector}"</c> resolves against the DataContext the control
    /// inherits from its host, so the control must never overwrite that DataContext: doing so
    /// leaves the binding reading from an object with no such property, and the picker ends up
    /// empty. Setting the property on a control with no parent never notices.
    /// </remarks>
    [AvaloniaFact]
    public void BoundToAHostsViewModel_ThePickerIsFilled()
    {
        LanguageSelectorViewModel selector = Build(out _);

        LanguageSelector control = new();
        control.Bind(LanguageSelector.ViewModelProperty,
            new Avalonia.Data.Binding(nameof(Host.LanguageSelector)));

        Window window = new()
        {
            DataContext = new Host(selector),
            Content = control,
            Width = 300,
            Height = 200
        };

        window.Show();

        Assert.Same(selector, control.ViewModel);

        ComboBox combo = control.GetVisualDescendants().OfType<ComboBox>().Single();

        Assert.NotNull(combo.ItemsSource);
        Assert.Equal(selector.AvailableLanguages.Count, combo.ItemsSource!.Cast<object>().Count());
        Assert.Same(selector.SelectedLanguage, combo.SelectedItem);
    }

    /// <summary>Handing it a DataContext is the other way in, and still has to work.</summary>
    [AvaloniaFact]
    public void GivenAsADataContext_ThePickerAdoptsIt()
    {
        LanguageSelectorViewModel selector = Build(out _);
        LanguageSelector control = new() { DataContext = selector };

        Window window = new() { Content = control, Width = 300, Height = 200 };
        window.Show();

        Assert.Same(selector, control.ViewModel);
        Assert.NotEmpty(control.GetVisualDescendants().OfType<ComboBox>().Single().ItemsSource!.Cast<object>());
    }
}

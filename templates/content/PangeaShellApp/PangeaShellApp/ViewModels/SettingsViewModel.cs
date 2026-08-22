using Avalonia.Styling;
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Localization.Controls;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PangeaShellApp.Services;
using System.ComponentModel;

namespace PangeaShellApp.ViewModels;

/// <summary>
/// Language and appearance, saved to disk and restored on the next run by
/// <see cref="AppStartupFeature"/>.
/// </summary>
/// <remarks>
/// The language applies the moment it is picked - that is what a language picker means, and the
/// window is already in the new language before Save is pressed. Saving records what is applied.
/// <para>
/// Also the screen that refuses to be left: <see cref="CanNavigateAwayAsync"/> is asked before any
/// navigation away, and answering <see langword="false"/> cancels it.
/// </para>
/// </remarks>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly IThemeService _theming;
    private readonly IDialogService _dialogs;
    private readonly AppSettingsStore _store;

    private AppSettings _saved;

    [Binding] private bool _isDark;
    [Binding(ReadOnly = true)] private string _status = "";

    public SettingsViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _localization = serviceProvider.GetRequiredService<ILocalizationService>();
        _theming = serviceProvider.GetRequiredService<IThemeService>();
        _dialogs = serviceProvider.GetRequiredService<IDialogService>();
        _store = serviceProvider.GetRequiredService<AppSettingsStore>();

        Strings = serviceProvider.GetRequiredService<LocalizedStrings>();
        LanguageSelector = serviceProvider.GetRequiredService<LanguageSelectorViewModel>();
        LanguageSelector.AutomationName = Strings["Settings_Language"];

        // Subscribed to the selector rather than to the localization service: both objects belong
        // to this screen and die with it, where the service outlives every screen there is.
        LanguageSelector.PropertyChanged += OnLanguageChanged;

        // What is on screen now is what a fresh screen shows, saved or not.
        _isDark = _theming.CurrentVariant == ThemeVariant.Dark;
        _saved = new AppSettings { Culture = _localization.CurrentCulture.Name, IsDark = _isDark };
    }

    public LocalizedStrings Strings { get; }

    /// <summary>Drives the toolkit's language picker. Changing it applies immediately.</summary>
    public LanguageSelectorViewModel LanguageSelector { get; }

    /// <summary>
    /// Reads <see cref="IsDark"/>, so the generator notifies it whenever that changes. The language
    /// half is announced by hand, because what it reads is the service rather than a property here.
    /// </summary>
    public bool HasUnsavedChanges =>
        !string.Equals(_localization.CurrentCulture.Name, _saved.Culture, StringComparison.OrdinalIgnoreCase) ||
        IsDark != _saved.IsDark;

    public RelayCommand SaveCommand => CreateCommand(SaveAsync, () => HasUnsavedChanges);

    /// <summary>
    /// Asked before the shell navigates away. The dialog is awaited here, so the navigation waits
    /// for the answer rather than happening behind it.
    /// </summary>
    public override async Task<bool> CanNavigateAwayAsync()
    {
        if (!HasUnsavedChanges) return true;

        return await _dialogs.ConfirmAsync(
            Strings["Settings_Discard_Title"],
            Strings["Settings_Discard_Message"],
            Strings["Settings_Discard_Confirm"],
            Strings["Settings_Discard_Cancel"]);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(LanguageSelectorViewModel.SelectedLanguage)) return;

        // The window is already in the new language; what changed here is whether it is saved.
        LanguageSelector.AutomationName = Strings["Settings_Language"];
        AnnounceSavedState();
    }

    private async Task SaveAsync()
    {
        AppSettings settings = new() { Culture = _localization.CurrentCulture.Name, IsDark = IsDark };

        await _store.SaveAsync(settings);

        // The language is already applied; the variant is what Save is still responsible for.
        _theming.SetVariant(settings.IsDark ? ThemeVariant.Dark : ThemeVariant.Light);

        _saved = settings;
        _status = Strings["Settings_Saved"];

        OnPropertyChanged(nameof(Status));
        AnnounceSavedState();
    }

    /// <summary>_saved is not a bound property, so what depends on it has to be announced.</summary>
    private void AnnounceSavedState()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveCommand.RaiseCanExecuteChanged();
    }
}

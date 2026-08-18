using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Localization.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CdCSharp.Pangea.Localization.Controls;

/// <summary>
/// Drives the language a user can pick from the supported cultures.
/// </summary>
/// <remarks>
/// Picking applies immediately, which is what a language picker means: there is no confirming step
/// between choosing a language and reading the window in it. Anything the application saves is
/// saving what is already applied.
/// </remarks>
public class LanguageSelectorViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly ILogger<LanguageSelectorViewModel> _logger;

    private LanguageOption? _selectedLanguage;
    private string _automationName = "Language";

    public LanguageSelectorViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _localization = serviceProvider.GetRequiredService<ILocalizationService>();
        _logger = serviceProvider.GetService<ILogger<LanguageSelectorViewModel>>()
                  ?? NullLogger<LanguageSelectorViewModel>.Instance;

        AvailableLanguages = new ObservableCollection<LanguageOption>(
            _localization.SupportedCultures.Select(culture => new LanguageOption(culture)));

        _selectedLanguage = Find(_localization.CurrentCulture);

        // The culture is not this control's to own: a settings file restored at startup, or another
        // screen, can change it, and a picker showing the wrong language is worse than no picker.
        _localization.CultureChanged += OnCultureChanged;
    }

    public ObservableCollection<LanguageOption> AvailableLanguages { get; }

    /// <summary>
    /// What a screen reader calls the picker. Settable, so an application can localize it.
    /// </summary>
    /// <remarks>
    /// The default is deliberately not localized: it is read in whatever language the window is
    /// currently in, and an application that cares will pass its own string.
    /// </remarks>
    public string AutomationName
    {
        get => _automationName;
        set
        {
            if (_automationName == value) return;

            _automationName = value;
            OnPropertyChanged();
        }
    }

    /// <summary>The language in use. Setting it changes the application's culture.</summary>
    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (ReferenceEquals(_selectedLanguage, value)) return;

            LanguageOption? previous = _selectedLanguage;
            _selectedLanguage = value;

            if (value is not null)
            {
                try
                {
                    _localization.SetCulture(value.Name);
                }
                catch (Exception ex)
                {
                    // Roll the picker back to what is actually applied rather than lie about it.
                    _logger.LogError(ex, "Could not switch to {Culture}", value.Name);
                    _selectedLanguage = previous;
                }
            }

            OnPropertyChanged();
        }
    }

    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    private LanguageOption? Find(CultureInfo culture) =>
        AvailableLanguages.FirstOrDefault(language =>
            string.Equals(language.Name, culture.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Follows a change made elsewhere. Assigns the field rather than the property, because setting
    /// the property would ask the service to do again what it has just finished doing.
    /// </summary>
    private void OnCultureChanged(object? sender, CultureChangedEventArgs e)
    {
        LanguageOption? applied = Find(e.CurrentCulture);

        if (ReferenceEquals(_selectedLanguage, applied)) return;

        _selectedLanguage = applied;
        OnPropertyChanged(nameof(SelectedLanguage));
    }
}

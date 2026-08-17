using Avalonia.Styling;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;

namespace CdCSharp.Pangea.Theming.Controls;

/// <summary>
/// Drives the two appearance axes: which theme is in use, and whether it shows light or dark.
/// </summary>
public class ThemeSelectorViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly ILogger<ThemeSelectorViewModel> _logger;

    private string _selectedTheme;
    private bool _isDark;

    public ThemeSelectorViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _themeService = serviceProvider.GetRequiredService<IThemeService>();
        _logger = serviceProvider.GetRequiredService<ILogger<ThemeSelectorViewModel>>();

        AvailableThemes = new ObservableCollection<string>(_themeService.AvailableThemes);
        _selectedTheme = _themeService.CurrentTheme;
        _isDark = _themeService.CurrentVariant == ThemeVariant.Dark;
    }

    public ObservableCollection<string> AvailableThemes { get; }

    /// <summary>Which palette pair is in use.</summary>
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value) return;

            string previous = _selectedTheme;
            _selectedTheme = value;

            try
            {
                _themeService.SetTheme(value);
            }
            catch (Exception ex)
            {
                // Roll the picker back to what is actually applied rather than lie about it.
                _logger.LogError(ex, "Could not apply theme {ThemeName}", value);
                _selectedTheme = previous;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(VariantTooltip));
        }
    }

    /// <summary>Whether the current theme shows its dark palette.</summary>
    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark == value) return;

            _isDark = value;
            _themeService.SetVariant(value ? ThemeVariant.Dark : ThemeVariant.Light);

            OnPropertyChanged();
            OnPropertyChanged(nameof(VariantIcon));
            OnPropertyChanged(nameof(VariantTooltip));
        }
    }

    public string VariantIcon => IsDark ? "🌙" : "☀️";

    public string VariantTooltip => $"{SelectedTheme} - {(IsDark ? "dark" : "light")}";

    public RelayCommand ToggleVariantCommand => CreateCommand(() => IsDark = !IsDark);

    /// <summary>Re-reads the themes registered with the service.</summary>
    public RelayCommand RefreshThemesCommand => CreateCommand(RefreshThemes);

    private void RefreshThemes()
    {
        AvailableThemes.Clear();

        foreach (string theme in _themeService.AvailableThemes)
        {
            AvailableThemes.Add(theme);
        }
    }
}

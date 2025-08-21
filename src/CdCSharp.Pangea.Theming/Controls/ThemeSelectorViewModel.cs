using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace CdCSharp.Pangea.Theming.Controls;

public partial class ThemeSelectorViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    [Binding] private string? _selectedTheme;

    public ThemeSelectorViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _themeService = serviceProvider.GetRequiredService<IThemeService>();

        AvailableThemes = new ObservableCollection<string>(_themeService.GetAvailableThemes());
        _selectedTheme = _themeService.GetCurrentTheme() ?? "Light";
    }

    public ObservableCollection<string> AvailableThemes { get; }

    public bool IsDarkSelected
    {
        get => SelectedTheme?.Equals("Dark", StringComparison.OrdinalIgnoreCase) == true;
        set 
        {
            string newTheme = value ? "Dark" : "Light";
            SelectedTheme = newTheme;
        }
    }

    public string ThemeIcon => IsDarkSelected ? "🌙" : "☀️";
    public string ThemeTooltip => $"Current theme: {SelectedTheme ?? "None"}";

    public RelayCommand RefreshThemesCommand => CreateCommand(RefreshThemes);

    partial void OnSelectedThemeChanged()
    {
        try
        {
            if (!string.IsNullOrEmpty(SelectedTheme))
                _themeService.SetCustomTheme(SelectedTheme);
            
            OnPropertyChanged(nameof(IsDarkSelected));
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(ThemeTooltip));
        }
        catch (Exception)
        {
            string? currentTheme = _themeService.GetCurrentTheme();
            if (_selectedTheme != currentTheme)
            {
                _selectedTheme = currentTheme;
                OnPropertyChanged(nameof(SelectedTheme));
                OnPropertyChanged(nameof(IsDarkSelected));
                OnPropertyChanged(nameof(ThemeIcon));
                OnPropertyChanged(nameof(ThemeTooltip));
            }
        }
    }
    private void RefreshThemes()
    {
        AvailableThemes.Clear();
        List<string> themes = _themeService.GetAvailableThemes();
        foreach (string theme in themes)
            AvailableThemes.Add(theme);
    }
}
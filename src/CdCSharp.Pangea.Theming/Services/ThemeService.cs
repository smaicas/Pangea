using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CdCSharp.Pangea.Theming.Services;

public class ThemeService : IThemeService
{
    public static class Resources
    {
        public const string DarkThemePath = "avares://CdCSharp.Pangea.Theming/Resources/Themes/DarkTheme.axaml";
        public const string LightThemePath = "avares://CdCSharp.Pangea.Theming/Resources/Themes/LightTheme.axaml";
    }

    public static class Themes
    {
        public const string Dark = nameof(Dark);
        public const string Light = nameof(Light);
    }

    private static readonly IReadOnlyDictionary<string, string> DefaultThemes = new Dictionary<string, string>
    {
        [Themes.Dark] = Resources.DarkThemePath, 
        [Themes.Light] = Resources.LightThemePath
    }.AsReadOnly();

    private readonly ConcurrentDictionary<string, string> _registeredThemes = new();
    private readonly object _themeOperationLock = new();

    private ResourceInclude? _currentCustomTheme;
    private string? _currentThemeName;

    public ThemeService()
    {
        InitializeDefaultThemes();
    }

    public PangeaUI? ToolKitUI { get; private set; }

    public void RegisterTheme(string name, string resourcePath)
    {
        ValidateThemeParameters(name, resourcePath);
        _registeredThemes[name] = resourcePath;
    }

    public void UnregisterTheme(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_registeredThemes.TryRemove(name, out _) && _currentThemeName == name)
            SetCustomTheme(null);
    }

    public IReadOnlyDictionary<string, string> GetRegisteredThemes() =>
        _registeredThemes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value).AsReadOnly();

    public bool IsThemeRegistered(string name) =>
        !string.IsNullOrWhiteSpace(name) && _registeredThemes.ContainsKey(name);

    public void SetCustomTheme(string? themeName)
    {
        if (_currentThemeName == themeName) return;

        lock (_themeOperationLock)
        {
            try
            {
                _currentThemeName = themeName;
                ApplyCustomTheme(themeName);
                SynchronizeWithAvaloniaThemeVariant(themeName);
            }
            catch (Exception)
            {
                _currentThemeName = null;
                throw;
            }
        }
    }

    public string? GetCurrentTheme() => _currentThemeName;

    public List<string> GetAvailableThemes() => _registeredThemes.Keys.ToList();

    public void RegisterToolkitUI(PangeaUI toolkitUI)
    {
        ArgumentNullException.ThrowIfNull(toolkitUI);
        
        ToolKitUI = toolkitUI;

        if (!string.IsNullOrEmpty(_currentThemeName))
            ApplyCustomTheme(_currentThemeName);
    }

    private void InitializeDefaultThemes()
    {
        foreach (KeyValuePair<string, string> theme in DefaultThemes)
            _registeredThemes[theme.Key] = theme.Value;
    }

    private static void ValidateThemeParameters(string name, string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Theme name cannot be null or empty", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("Resource path cannot be null or empty", nameof(resourcePath));
        }
    }

    private void ApplyCustomTheme(string? themeName)
    {
        if (Application.Current?.Resources?.MergedDictionaries == null) return;

        RemoveCurrentCustomTheme(); 

        if (!string.IsNullOrEmpty(themeName))
            LoadCustomTheme(themeName);
    }

    private void LoadCustomTheme(string themeName)
    {
        if (!_registeredThemes.TryGetValue(themeName, out string? resourcePath))
        {
            throw new InvalidOperationException($"Theme '{themeName}' is not registered");
        }

        try
        {
            
            Uri themeUri = new(resourcePath);
            _currentCustomTheme = new ResourceInclude(themeUri) { Source = themeUri };
            Application.Current!.Resources!.MergedDictionaries.Add(_currentCustomTheme);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load theme '{themeName}' from '{resourcePath}'", ex);
        }
    }

    private void RemoveCurrentCustomTheme()
    {
        if (_currentCustomTheme != null && Application.Current?.Resources?.MergedDictionaries != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_currentCustomTheme);
            _currentCustomTheme = null;
        }
    }

    private static void SynchronizeWithAvaloniaThemeVariant(string? themeName)
    {
        try
        {
            Application? app = Application.Current;
            if (app == null) return;

            ThemeVariant targetVariant = themeName?.Contains("dark", StringComparison.OrdinalIgnoreCase) == true
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            app.RequestedThemeVariant = targetVariant;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SynchronizeWithAvaloniaThemeVariant error: {ex.Message}");
        }
    }
}
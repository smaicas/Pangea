using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace CdCSharp.Pangea.Theming;

public class PangeaUI : Styles
{
    public static readonly StyledProperty<string?> CustomThemeProperty =
        AvaloniaProperty.Register<PangeaUI, string?>(nameof(CustomTheme));

    private readonly IThemeService? _themeService;

    public PangeaUI(IServiceProvider? serviceProvider = null, ThemingOptions? themingConfig = null)
    {
        Debug.WriteLine("DEBUG: Iniciando constructor ToolkitUI");

        try
        {
            Debug.WriteLine($"DEBUG: ServiceProvider recibido: {serviceProvider != null}");

            if (serviceProvider != null)
            {
                Debug.WriteLine("DEBUG: Obteniendo IThemeService desde ServiceProvider...");
                _themeService = serviceProvider.GetService<IThemeService>();
                Debug.WriteLine($"DEBUG: IThemeService obtenido: {_themeService != null}");
            }
            else
            {
                Debug.WriteLine("DEBUG: ServiceProvider es null, _themeService será null");
                _themeService = null;
            }

            Debug.WriteLine("DEBUG: Intentando cargar XAML con AvaloniaXamlLoader.Load...");
            AvaloniaXamlLoader.Load(serviceProvider, this);
            Debug.WriteLine("DEBUG: XAML cargado exitosamente");

            Debug.WriteLine("DEBUG: Registrando ToolkitUI con ThemeService...");
            _themeService?.RegisterToolkitUI(this);
            Debug.WriteLine("DEBUG: ToolkitUI registrado con ThemeService");

            if (themingConfig != null)
            {
                Debug.WriteLine("DEBUG: Aplicando configuración de theming...");
                ApplyConfiguration(themingConfig);
                Debug.WriteLine("DEBUG: Configuración de theming aplicada");
            }
            else
                Debug.WriteLine("DEBUG: No hay configuración de theming para aplicar");

            Debug.WriteLine("DEBUG: Constructor ToolkitUI completado exitosamente");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DEBUG: Error en constructor ToolkitUI: {ex.Message}");
            Debug.WriteLine($"DEBUG: Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public string? CustomTheme
    {
        get => GetValue(CustomThemeProperty);
        set => SetValue(CustomThemeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == CustomThemeProperty && _themeService != null)
        {
            string? themeName = change.NewValue as string;
            Debug.WriteLine($"DEBUG: PangeaUI CustomTheme cambiado a: {themeName}");
            _themeService.SetCustomTheme(themeName);
        }

        base.OnPropertyChanged(change);
    }

    private void ApplyConfiguration(ThemingOptions config)
    {
        RegisterCustomThemes(config);
        ApplyDefaultTheme(config);
    }

    private void RegisterCustomThemes(ThemingOptions config)
    {
        if (_themeService == null) return;

        foreach (KeyValuePair<string, string> theme in config.CustomThemes)
            _themeService.RegisterTheme(theme.Key, theme.Value);
    }

    private void ApplyDefaultTheme(ThemingOptions config)
    {
        if (CustomTheme != null) return;

        string defaultTheme = DetermineDefaultTheme(config);
        CustomTheme = defaultTheme;
    }

    private static string DetermineDefaultTheme(ThemingOptions config)
    {
        if (!string.IsNullOrEmpty(config.DefaultTheme))
            return config.DefaultTheme;

        if (config.EnableSystemThemeDetection)
        {
            string? systemTheme = DetectSystemTheme();
            if (!string.IsNullOrEmpty(systemTheme))
                return systemTheme;
        }

        return config.FallbackTheme;
    }

    private static string? DetectSystemTheme()
    {
        try
        {
            ThemeVariant? currentVariant = Application.Current?.RequestedThemeVariant;
            return currentVariant switch
            {
                not null when currentVariant == ThemeVariant.Dark => "Dark",
                not null when currentVariant == ThemeVariant.Light => "Light",
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
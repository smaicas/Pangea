namespace CdCSharp.Pangea.Theming;

public class ThemingOptions
{
    public static ThemingOptions Default => new()
    {
        EnableSystemThemeDetection = true,
        SelectedTheme = PangeaThemeVariant.Variant.Auto,
        HasUserSelection = false,
        CustomThemes = new Dictionary<string, string>(),
        DefaultTheme = "Dark",
    };
    public string? DefaultTheme { get; set; }
    public string FallbackTheme { get; set; } = "Dark";

    public bool EnableSystemThemeDetection { get; set; } = true;
    public PangeaThemeVariant.Variant SelectedTheme { get; set; } = PangeaThemeVariant.Variant.Auto;
    public bool HasUserSelection { get; set; } = false;
    public Dictionary<string, string> CustomThemes { get; set; } = new();
}

public class PangeaThemeVariant
{
    public enum Variant
    {
        Light,
        Dark,
        Auto
    }

    public static Variant FromThemeVariant(object? avaloniaVariant)
    {
        if (avaloniaVariant is Avalonia.Styling.ThemeVariant themeVariant)
        {
            return themeVariant switch
            {
                _ when themeVariant == Avalonia.Styling.ThemeVariant.Light => PangeaThemeVariant.Variant.Light,
                _ when themeVariant == Avalonia.Styling.ThemeVariant.Dark => PangeaThemeVariant.Variant.Dark,
                _ => PangeaThemeVariant.Variant.Auto
            };
        }

        return PangeaThemeVariant.Variant.Auto;
    }

    public static Variant FromPlatformThemeVariant(Avalonia.Platform.PlatformThemeVariant platformVariant)
    {
        return platformVariant switch
        {
            Avalonia.Platform.PlatformThemeVariant.Light => PangeaThemeVariant.Variant.Light,
            Avalonia.Platform.PlatformThemeVariant.Dark => PangeaThemeVariant.Variant.Dark,
            _ => PangeaThemeVariant.Variant.Light // Default fallback
        };
    }

    public static Variant FromString(string name)
    {
        return name switch
        {
            "Dark" => PangeaThemeVariant.Variant.Dark,
            "Light" => PangeaThemeVariant.Variant.Light,
            "Auto" => PangeaThemeVariant.Variant.Auto,
            _ => PangeaThemeVariant.Variant.Auto
        };
    }
}

public class ThemeDefinition
{
    public string Name { get; }
    public PangeaThemeVariant.Variant Category { get; }
    public string ResourcePath { get; }

    public ThemeDefinition(string name, PangeaThemeVariant.Variant category, string resourcePath)
    {
        Name = name;
        Category = category;
        ResourcePath = resourcePath;
    }

    public override string ToString() => Name;
}

public class ThemeChangedEventArgs : EventArgs
{
    public ThemeDefinition? PreviousTheme { get; }
    public ThemeDefinition? NewTheme { get; }

    public ThemeChangedEventArgs(ThemeDefinition? previousTheme, ThemeDefinition? newTheme)
    {
        PreviousTheme = previousTheme;
        NewTheme = newTheme;
    }
}
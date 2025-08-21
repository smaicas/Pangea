using Avalonia.Styling;

namespace CdCSharp.Pangea.Theming.Abstractions;

public interface IThemeService
{
    PangeaUI? ToolKitUI { get; }
    void RegisterTheme(string name, string resourcePath);
    void UnregisterTheme(string name);
    IReadOnlyDictionary<string, string> GetRegisteredThemes();
    bool IsThemeRegistered(string name);
    void SetCustomTheme(string? themeName);
    string? GetCurrentTheme();
    List<string> GetAvailableThemes();
    void RegisterToolkitUI(PangeaUI toolkitUI);
}
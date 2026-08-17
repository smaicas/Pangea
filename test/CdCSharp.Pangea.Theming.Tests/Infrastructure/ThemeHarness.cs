using Avalonia;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Services;

namespace CdCSharp.Pangea.Theming.Tests.Infrastructure;

/// <summary>
/// Applies the toolkit theme the way an application does, through <see cref="ThemeService"/>.
/// </summary>
public static class ThemeHarness
{
    private static ThemeService? _service;
    private static Application? _owner;

    public static IReadOnlyList<ThemeVariant> Variants { get; } = [ThemeVariant.Light, ThemeVariant.Dark];

    public static ThemeVariant Variant(string name) => name switch
    {
        "Dark" => ThemeVariant.Dark,
        "Light" => ThemeVariant.Light,
        _ => ThemeVariant.Default
    };

    /// <summary>Ensures the running application has the toolkit palette merged.</summary>
    public static ThemeService Service
    {
        get
        {
            // The headless harness can hand each test its own Application, and a ThemeService
            // remembers which one it merged into, so it is rebuilt whenever the application changes.
            if (!ReferenceEquals(_owner, Application.Current))
            {
                _owner = Application.Current;
                _service = new ThemeService();
                _service.SetTheme(PangeaTheme.DefaultName);
            }

            return _service!;
        }
    }

    public static void ApplyVariant(ThemeVariant variant) => Service.SetVariant(variant);

    /// <summary>Resolves a key against the running application for the given variant.</summary>
    public static bool TryResolve(string key, ThemeVariant variant, out object? value)
    {
        _ = Service;
        return Application.Current!.TryGetResource(key, variant, out value);
    }
}

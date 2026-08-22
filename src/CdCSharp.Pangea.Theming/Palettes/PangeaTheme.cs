using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System.Reflection;

namespace CdCSharp.Pangea.Theming.Palettes;

/// <summary>
/// Turns a pair of palettes into the resource dictionary the control themes read from.
/// </summary>
/// <remarks>
/// <para>
/// A theme is a light palette and a dark one, published as Avalonia theme variants so a
/// ThemeVariantScope can render a subtree in the opposite variant.
/// </para>
/// <para>
/// Nothing here generates XAML. An Avalonia <see cref="ResourceDictionary"/> is an ordinary object,
/// so the palette is materialised directly - which is why a theme can be a class at all.
/// </para>
/// </remarks>
/// <param name="light">The palette shown under the light variant.</param>
/// <param name="dark">The palette shown under the dark variant.</param>
/// <param name="metrics">
/// Sizes for every control, defaulting to <see cref="ThemeMetrics.Values"/>. Pass
/// <see cref="ThemeMetrics.Touch"/> for an application driven by a thumb.
/// </param>
public class PangeaTheme(PangeaPalette light, PangeaPalette dark, IReadOnlyDictionary<string, object>? metrics = null)
{
    /// <summary>Name the toolkit's own theme is registered under.</summary>
    public const string DefaultName = "Pangea";

    /// <summary>The toolkit's warm minimal palettes.</summary>
    public static PangeaTheme Default { get; } = new(new LightPalette(), new DarkPalette());

    /// <summary>
    /// Brushes that do not follow the <c>...Color</c> to <c>...Brush</c> rule: a different name, a
    /// literal colour, or a colour used at reduced opacity.
    /// </summary>
    private static readonly BrushOverride[] Overrides =
    [
        new("ThemeControlTransparentBrush", Colour: Colors.Transparent),
        new("RefreshVisualizerBackground", Colour: Colors.Transparent),
        new("RefreshVisualizerForeground", From: "ThemeForegroundColor"),
        new("CaptionButtonForeground", From: "WindowChromeForegroundColor"),
        new("CaptionButtonBackground", From: "WindowChromeBackgroundColor"),
        new("CaptionButtonBorderBrush", From: "ThemeBorderMidColor"),
        new("NotificationCardBackgroundBrush", From: "NotificationBackgroundColor", Opacity: 0.95),
        new("NotificationCardInformationBackgroundBrush", From: "NotificationInfoBackgroundColor", Opacity: 0.95),
        new("NotificationCardSuccessBackgroundBrush", From: "NotificationSuccessBackgroundColor", Opacity: 0.95),
        new("NotificationCardWarningBackgroundBrush", From: "NotificationWarningBackgroundColor", Opacity: 0.95),
        new("NotificationCardErrorBackgroundBrush", From: "NotificationErrorBackgroundColor", Opacity: 0.95),
        new("DatePickerFlyoutPresenterHighlightFill", From: "ThemeAccentColor", OpacityFrom: p => p.PickerHighlightOpacity),
        new("TimePickerFlyoutPresenterHighlightFill", From: "ThemeAccentColor", OpacityFrom: p => p.PickerHighlightOpacity)
    ];

    /// <summary>Keys that are just another name for an entry already in the dictionary.</summary>
    private static readonly (string Alias, string Target)[] Aliases =
    [
        ("ThemeDisabledColor", "ThemeForegroundLowColor"),
        ("TitleBarBackgroundBrush", "ThemeBackgroundBrush"),
        ("DatePickerSpacerFill", "ThemeBorderMidBrush"),
        ("TimePickerSpacerFill", "ThemeBorderMidBrush")
    ];

    /// <summary>Colour properties of a palette, cached per type.</summary>
    private static readonly Dictionary<Type, PropertyInfo[]> ColourProperties = [];

    public PangeaPalette Light { get; } = light ?? throw new ArgumentNullException(nameof(light));

    public PangeaPalette Dark { get; } = dark ?? throw new ArgumentNullException(nameof(dark));

    /// <summary>Sizes for every control, shared by both variants.</summary>
    public IReadOnlyDictionary<string, object> Metrics { get; } = metrics ?? ThemeMetrics.Values;

    /// <summary>
    /// Builds the dictionary: one entry per variant, plus the metrics, which do not vary.
    /// </summary>
    public ResourceDictionary Build()
    {
        ResourceDictionary theme = new();

        // Default is what a lookup falls back to when no entry matches the requested variant.
        // Each slot gets its own dictionary: Avalonia gives a ResourceDictionary a single owner,
        // so sharing one instance across two slots throws when the second one is attached.
        theme.ThemeDictionaries[ThemeVariant.Default] = BuildVariant(Light);
        theme.ThemeDictionaries[ThemeVariant.Light] = BuildVariant(Light);
        theme.ThemeDictionaries[ThemeVariant.Dark] = BuildVariant(Dark);

        foreach (KeyValuePair<string, object> metric in Metrics)
        {
            theme[metric.Key] = metric.Value;
        }

        return theme;
    }

    /// <summary>
    /// Every colour becomes a resource under its own property name, plus a brush of the same name
    /// with "Color" swapped for "Brush". A palette that adds a colour gets its brush for free.
    /// </summary>
    public static ResourceDictionary BuildVariant(PangeaPalette palette)
    {
        ResourceDictionary resources = new();
        Dictionary<string, Color> colours = [];

        foreach (PropertyInfo property in GetColourProperties(palette.GetType()))
        {
            Color colour = (Color)property.GetValue(palette)!;
            colours[property.Name] = colour;

            resources[property.Name] = colour;
            resources[property.Name.Replace("Color", "Brush")] = new SolidColorBrush(colour);
        }

        foreach (BrushOverride entry in Overrides)
        {
            Color colour = entry.Colour ?? colours[entry.From!];
            double opacity = entry.OpacityFrom?.Invoke(palette) ?? entry.Opacity;
            resources[entry.Key] = new SolidColorBrush(colour, opacity);
        }

        foreach ((string alias, string target) in Aliases)
        {
            resources[alias] = resources[target]!;
        }

        return resources;
    }

    private static PropertyInfo[] GetColourProperties(Type paletteType)
    {
        lock (ColourProperties)
        {
            if (ColourProperties.TryGetValue(paletteType, out PropertyInfo[]? cached)) return cached;

            PropertyInfo[] properties = paletteType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.PropertyType == typeof(Color) && property.CanRead)
                .ToArray();

            ColourProperties[paletteType] = properties;
            return properties;
        }
    }

    private sealed record BrushOverride(
        string Key,
        string? From = null,
        Color? Colour = null,
        double Opacity = 1d,
        Func<PangeaPalette, double>? OpacityFrom = null);
}

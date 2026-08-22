using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdCSharp.Pangea.Theming.Palettes;

/// <summary>
/// Sizes, spacings and fonts. These do not change between light and dark, so they live once at the
/// root of the theme instead of being duplicated in every palette.
/// </summary>
/// <remarks>
/// <para>
/// Every control theme reads these rather than hardcoding a size, so the set is the one place the
/// density of the whole application is decided. <see cref="Values"/> is sized for a pointer;
/// <see cref="Touch"/> is the same set sized for a thumb.
/// </para>
/// <para>
/// An application picks one when it builds its theme, and overrides individual keys by declaring
/// them in <c>Application.Resources</c> - application resources are consulted before application
/// styles, and the theme is a style.
/// </para>
/// </remarks>
public static class ThemeMetrics
{
    public static IReadOnlyDictionary<string, object> Values { get; } = new Dictionary<string, object>
    {
        ["ContentControlThemeFontFamily"] = new FontFamily("fonts:Inter#Inter, $Default"),
        ["ThemeBorderThickness"] = Thickness.Parse("1"),
        ["ThemeDisabledOpacity"] = 0.4d,
        ["FontSizeSmall"] = 12d,
        ["FontSizeNormal"] = 14d,
        ["FontSizeLarge"] = 18d,
        ["ScrollBarThickness"] = 16d,
        ["ScrollBarThumbThickness"] = 10d,
        ["IconElementThemeHeight"] = 20d,
        ["IconElementThemeWidth"] = 20d,
        ["TextControlPlaceholderOpacity"] = 0.5d,
        ["ButtonMinHeight"] = 32d,
        ["ButtonMinWidth"] = 64d,
        ["ButtonPadding"] = Thickness.Parse("12,6"),
        ["ButtonCornerRadius"] = CornerRadius.Parse("6"),
        ["TextBoxMinHeight"] = 32d,
        ["TextBoxPadding"] = Thickness.Parse("8,6"),
        ["TextBoxCornerRadius"] = CornerRadius.Parse("4"),
        ["ComboBoxMinHeight"] = 32d,
        ["ComboBoxPadding"] = Thickness.Parse("8,6"),
        ["ComboBoxCornerRadius"] = CornerRadius.Parse("4"),
        ["ComboBoxItemMinHeight"] = 32d,
        ["ComboBoxItemPadding"] = Thickness.Parse("8,6"),
        ["ListBoxItemMinHeight"] = 32d,
        ["ListBoxItemPadding"] = Thickness.Parse("8,6"),
        ["SliderTrackHeight"] = 4d,
        ["SliderThumbSize"] = 18d,
        ["SliderTrackCornerRadius"] = CornerRadius.Parse("2"),
        ["ProgressBarHeight"] = 8d,
        ["ProgressBarCornerRadius"] = CornerRadius.Parse("4"),
        ["CheckBoxSize"] = 18d,
        ["RadioButtonSize"] = 18d,
        ["CheckBoxCornerRadius"] = CornerRadius.Parse("3"),
        ["CheckBoxGlyphSize"] = 12d,
        ["RadioButtonGlyphSize"] = 10d,
        ["TabItemPadding"] = Thickness.Parse("12,8"),
        ["TabItemMinHeight"] = 36d,
        ["TabItemCornerRadius"] = CornerRadius.Parse("6,6,0,0"),
        ["ExpanderMinHeight"] = 40d,
        ["ExpanderPadding"] = Thickness.Parse("12,10"),
        ["CalendarButtonSize"] = 40d,
        ["CalendarButtonCornerRadius"] = CornerRadius.Parse("4"),
        ["MenuItemPadding"] = Thickness.Parse("8,6"),
        ["MenuItemMinHeight"] = 28d,
        ["MenuItemCornerRadius"] = CornerRadius.Parse("4"),
        ["NotificationCornerRadius"] = CornerRadius.Parse("8"),
        ["NotificationPadding"] = Thickness.Parse("16"),
        ["NotificationBorderThickness"] = Thickness.Parse("1"),
    };

    /// <summary>
    /// <see cref="Values"/> resized for fingers: nothing tappable below 48, and the type a step up
    /// to match.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 48 is the number both platforms landed on independently - Material's touch target and, near
    /// enough, Apple's 44pt - because a thumb pad is about 9mm across and a target smaller than it
    /// is one somebody misses while walking. A pointer is a single pixel and needs none of it,
    /// which is why this is a second set rather than a bigger default.
    /// </para>
    /// <para>
    /// Handed to a theme, so a phone application is a constructor argument rather than a screen's
    /// worth of style overrides:
    /// </para>
    /// <code>
    /// options.Themes[PangeaTheme.DefaultName] =
    ///     new PangeaTheme(new AppLightPalette(), new AppDarkPalette(), ThemeMetrics.Touch);
    /// </code>
    /// <para>
    /// Corner radii grow with the controls: the same 4px on a 48-high field reads as a rounding
    /// error rather than as a shape.
    /// </para>
    /// </remarks>
    public static IReadOnlyDictionary<string, object> Touch { get; } = Resize(new Dictionary<string, object>
    {
        ["FontSizeSmall"] = 13d,
        ["FontSizeNormal"] = 16d,
        ["FontSizeLarge"] = 20d,
        ["IconElementThemeHeight"] = 24d,
        ["IconElementThemeWidth"] = 24d,
        ["ButtonMinHeight"] = 48d,
        ["ButtonMinWidth"] = 88d,
        ["ButtonPadding"] = Thickness.Parse("20,12"),
        ["ButtonCornerRadius"] = CornerRadius.Parse("12"),
        ["TextBoxMinHeight"] = 48d,
        ["TextBoxPadding"] = Thickness.Parse("14,12"),
        ["TextBoxCornerRadius"] = CornerRadius.Parse("12"),
        ["ComboBoxMinHeight"] = 48d,
        ["ComboBoxPadding"] = Thickness.Parse("14,12"),
        ["ComboBoxCornerRadius"] = CornerRadius.Parse("12"),
        ["ComboBoxItemMinHeight"] = 48d,
        ["ComboBoxItemPadding"] = Thickness.Parse("14,12"),
        ["ListBoxItemMinHeight"] = 48d,
        ["ListBoxItemPadding"] = Thickness.Parse("14,12"),
        ["SliderTrackHeight"] = 6d,
        ["SliderThumbSize"] = 28d,
        ["SliderTrackCornerRadius"] = CornerRadius.Parse("3"),
        ["ProgressBarHeight"] = 10d,
        ["ProgressBarCornerRadius"] = CornerRadius.Parse("5"),
        // The box stays a box; what grows is the area around it, which the control theme takes from
        // the padding. A 24 checkbox next to 16pt text is a checkbox that outweighs its own label.
        ["CheckBoxSize"] = 24d,
        ["RadioButtonSize"] = 24d,
        ["CheckBoxCornerRadius"] = CornerRadius.Parse("6"),
        ["CheckBoxGlyphSize"] = 16d,
        ["RadioButtonGlyphSize"] = 14d,
        ["TabItemPadding"] = Thickness.Parse("16,12"),
        ["TabItemMinHeight"] = 48d,
        ["TabItemCornerRadius"] = CornerRadius.Parse("12,12,0,0"),
        ["ExpanderMinHeight"] = 56d,
        ["ExpanderPadding"] = Thickness.Parse("16,14"),
        ["CalendarButtonSize"] = 48d,
        ["CalendarButtonCornerRadius"] = CornerRadius.Parse("8"),
        ["MenuItemPadding"] = Thickness.Parse("16,12"),
        ["MenuItemMinHeight"] = 48d,
        ["MenuItemCornerRadius"] = CornerRadius.Parse("8"),
        ["NotificationCornerRadius"] = CornerRadius.Parse("16"),
        ["NotificationPadding"] = Thickness.Parse("20"),
    });

    /// <summary>
    /// <see cref="Values"/> with <paramref name="overrides"/> applied over it.
    /// </summary>
    /// <remarks>
    /// A density is the whole set or it is a set with holes in it: a theme built from a dictionary
    /// missing a key renders that control unstyled. Starting from the defaults means a metric added
    /// later is inherited rather than forgotten.
    /// </remarks>
    public static IReadOnlyDictionary<string, object> Resize(IReadOnlyDictionary<string, object> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        Dictionary<string, object> resized = new(Values);

        foreach (KeyValuePair<string, object> entry in overrides)
        {
            resized[entry.Key] = entry.Value;
        }

        return resized;
    }
}

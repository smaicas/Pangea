using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CdCSharp.Pangea.Theming.Palettes;

/// <summary>
/// Sizes, spacings and fonts. These do not change between light and dark, so they live once at the
/// root of the theme instead of being duplicated in every palette.
/// </summary>
public static class ThemeMetrics
{
    public static IReadOnlyDictionary<string, object> Values { get; } = new Dictionary<string, object>
    {
        ["ContentControlThemeFontFamily"] = new FontFamily("fonts:Inter#Inter, $Default"),
        ["ThemeBorderThickness"] = Thickness.Parse("1"),
        ["ThemeDisabledOpacity"] = 0.4d,
        ["FontSizeSmall"] = 10d,
        ["FontSizeNormal"] = 12d,
        ["FontSizeLarge"] = 16d,
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
        ["SliderTrackHeight"] = 4d,
        ["SliderThumbSize"] = 18d,
        ["SliderTrackCornerRadius"] = CornerRadius.Parse("2"),
        ["ProgressBarHeight"] = 6d,
        ["ProgressBarCornerRadius"] = CornerRadius.Parse("3"),
        ["CheckBoxSize"] = 18d,
        ["RadioButtonSize"] = 18d,
        ["CheckBoxCornerRadius"] = CornerRadius.Parse("3"),
        ["RadioButtonCornerRadius"] = CornerRadius.Parse("9"),
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
}

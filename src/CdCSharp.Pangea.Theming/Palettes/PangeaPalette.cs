using Avalonia.Media;

namespace CdCSharp.Pangea.Theming.Palettes;

/// <summary>
/// The colours a Pangea theme is made of. Inherit and override what you want to change.
/// </summary>
/// <remarks>
/// <para>
/// Every property name is the resource key it produces, so <c>ThemeBackgroundColor</c> is what a
/// control dictionary asks for with <c>{DynamicResource ThemeBackgroundColor}</c>. Each colour
/// also produces a matching <c>...Brush</c>, which is what most of the theme actually binds to.
/// </para>
/// <para>
/// The defaults are the light palette, so a theme that only cares about a handful of colours can
/// override just those.
/// </para>
/// </remarks>
public abstract class PangeaPalette
{
    /// <summary>Opacity of the date and time picker highlight fill.</summary>
    public virtual double PickerHighlightOpacity => 0.2d;


    // BASE COLORS - LIGHT WARM MINIMAL
    public virtual Color ThemeBackgroundColor => Color.Parse("#FFFBF9F4");
    public virtual Color ThemeForegroundColor => Color.Parse("#FF2A251F");

    // Border Hierarchy
    public virtual Color ThemeBorderLowColor => Color.Parse("#FFE2D5C4");
    public virtual Color ThemeBorderMidColor => Color.Parse("#FFD4C0A3");
    public virtual Color ThemeBorderHighColor => Color.Parse("#FFC6AB82");
    public virtual Color ThemeBorderVeryHighColor => Color.Parse("#FFB89661");

    // Control Background Hierarchy
    public virtual Color ThemeControlLowColor => Color.Parse("#FFF6F2EA");
    public virtual Color ThemeControlMidColor => Color.Parse("#FFF1EAD8");
    public virtual Color ThemeControlMidHighColor => Color.Parse("#FFECE2C6");
    public virtual Color ThemeControlHighColor => Color.Parse("#FFE7DAB4");
    public virtual Color ThemeControlVeryHighColor => Color.Parse("#FFE2D2A2");

    // Highlight/Hover States
    public virtual Color ThemeControlHighlightLowColor => Color.Parse("#FFF3EFE7");
    public virtual Color ThemeControlHighlightMidColor => Color.Parse("#FFEEE7D5");
    public virtual Color ThemeControlHighlightHighColor => Color.Parse("#FFE9DFC3");

    // Text Hierarchy
    public virtual Color ThemeForegroundLowColor => Color.Parse("#FF5C523E");

    // Texto deshabilitado (lo consume TabbedPage en Avalonia 12)
    public virtual Color ThemeForegroundMidColor => Color.Parse("#FF3A342A");
    public virtual Color ThemeForegroundHighColor => Color.Parse("#FF2A251F");

    // Accent Colors
    public virtual Color ThemeAccentColor => Color.Parse("#FF8B5A3C");
    public virtual Color ThemeAccentLightColor => Color.Parse("#FFA66B47");
    public virtual Color ThemeAccentDarkColor => Color.Parse("#FF7A4D31");

    // State Colors
    public virtual Color HighlightColor => Color.Parse("#FF8B5A3C");
    public virtual Color HighlightColor2 => Color.Parse("#FF7A4D31");
    public virtual Color HyperlinkColor => Color.Parse("#FF6B4528");
    public virtual Color HyperlinkVisitedColor => Color.Parse("#FF9B6B8C");

    // Status Colors
    public virtual Color ErrorColor => Color.Parse("#FFCC5A5A");
    public virtual Color WarningColor => Color.Parse("#FFE0A347");
    public virtual Color SuccessColor => Color.Parse("#FF6B9E4A");
    public virtual Color InfoColor => Color.Parse("#FF4A7BA7");

    // Interactive States
    public virtual Color ButtonHoverColor => Color.Parse("#FFE0D6C0");
    public virtual Color ButtonPressedColor => Color.Parse("#FFDBCEB0");
    public virtual Color ButtonDisabledColor => Color.Parse("#FFF1EAD8");
    public virtual Color InputHoverColor => Color.Parse("#FFEAE1CF");
    public virtual Color InputFocusColor => Color.Parse("#FFE5DBBD");
    public virtual Color InputDisabledColor => Color.Parse("#FFF3EFE7");

    // Selection States
    public virtual Color SelectionColor => Color.Parse("#FF8B5A3C");
    public virtual Color SelectionBackgroundColor => Color.Parse("#338B5A3C");
    public virtual Color SelectionHoverColor => Color.Parse("#668B5A3C");

    // ScrollBar Colors
    public virtual Color ScrollBarTrackColor => Color.Parse("#FFF1EAD8");
    public virtual Color ScrollBarThumbColor => Color.Parse("#FFE2D2A2");
    public virtual Color ScrollBarThumbHoverColor => Color.Parse("#FFD4C0A3");

    // Window Chrome Colors
    public virtual Color WindowChromeBackgroundColor => Color.Parse("#FFF6F2EA");
    public virtual Color WindowChromeForegroundColor => Color.Parse("#FF2A251F");

    // Menu Colors
    public virtual Color MenuBackgroundColor => Color.Parse("#FFF6F2EA");
    public virtual Color MenuBorderColor => Color.Parse("#FFD4C0A3");
    public virtual Color MenuItemHoverColor => Color.Parse("#FFEEE7D5");
    public virtual Color MenuSeparatorColor => Color.Parse("#FFD4C0A3");

    // Tooltip Colors
    public virtual Color TooltipBackgroundColor => Color.Parse("#FFF1EAD8");
    public virtual Color TooltipForegroundColor => Color.Parse("#FF2A251F");
    public virtual Color TooltipBorderColor => Color.Parse("#FFD4C0A3");

    // Calendar Colors
    public virtual Color CalendarBackgroundColor => Color.Parse("#FFF6F2EA");
    public virtual Color CalendarHeaderBackgroundColor => Color.Parse("#FFF1EAD8");
    public virtual Color CalendarButtonHoverColor => Color.Parse("#FFEEE7D5");
    public virtual Color CalendarButtonTodayColor => Color.Parse("#338B5A3C");
    public virtual Color CalendarButtonSelectedColor => Color.Parse("#FF8B5A3C");

    // Notification Colors
    public virtual Color NotificationBackgroundColor => Color.Parse("#FFF6F2EA");
    public virtual Color NotificationInfoBackgroundColor => Color.Parse("#FFE8F2F0");
    public virtual Color NotificationSuccessBackgroundColor => Color.Parse("#FFF0F5E8");
    public virtual Color NotificationWarningBackgroundColor => Color.Parse("#FFFBF2E3");
    public virtual Color NotificationErrorBackgroundColor => Color.Parse("#FFF8EBE8");

    // Tab Colors
    public virtual Color TabBackgroundColor => Color.Parse("#FFF6F2EA");
    public virtual Color TabSelectedBackgroundColor => Color.Parse("#FFFBF9F4");
    public virtual Color TabHoverBackgroundColor => Color.Parse("#FFEEE7D5");
    public virtual Color TabBorderColor => Color.Parse("#FFD4C0A3");

    // DataGrid Colors
    public virtual Color DataGridHeaderBackgroundColor => Color.Parse("#FFF1EAD8");
    public virtual Color DataGridRowHoverColor => Color.Parse("#FFF3EFE7");
    public virtual Color DataGridRowSelectedColor => Color.Parse("#338B5A3C");
    public virtual Color DataGridBorderColor => Color.Parse("#FFD4C0A3");

    // Expander Colors
    public virtual Color ExpanderHeaderBackgroundColor => Color.Parse("#FFF1EAD8");
    public virtual Color ExpanderHeaderHoverColor => Color.Parse("#FFEEE7D5");
    public virtual Color ExpanderContentBackgroundColor => Color.Parse("#FFF6F2EA");

    // Flyout Colors
    public virtual Color FlyoutBackgroundColor => Color.Parse("#FFF6F2EA");
    public virtual Color FlyoutBorderColor => Color.Parse("#FFD4C0A3");

    // GLOBAL THEME COLORS (OVERRIDE DEFAULTS)
    public virtual Color ThemeAccentColor2 => Color.Parse("#B38B5A3C");
    public virtual Color ThemeAccentColor3 => Color.Parse("#668B5A3C");
    public virtual Color ThemeAccentColor4 => Color.Parse("#338B5A3C");
    public virtual Color HighlightForegroundColor => Color.Parse("#FFFBF9F4");
    public virtual Color ErrorLowColor => Color.Parse("#1ACC5A5A");
}

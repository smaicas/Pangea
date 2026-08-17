using Avalonia.Media;

namespace CdCSharp.Pangea.Theming.Palettes;

/// <summary>Dark warm minimal.</summary>
public class DarkPalette : PangeaPalette
{
    public override double PickerHighlightOpacity => 0.3d;


    // BASE COLORS - LIGHT WARM MINIMAL
    public override Color ThemeBackgroundColor => Color.Parse("#FF1A1612");
    public override Color ThemeForegroundColor => Color.Parse("#FFFAF8F5");

    // Border Hierarchy
    public override Color ThemeBorderLowColor => Color.Parse("#FF2B2520");
    public override Color ThemeBorderMidColor => Color.Parse("#FF3A342C");
    public override Color ThemeBorderHighColor => Color.Parse("#FF4A4138");
    public override Color ThemeBorderVeryHighColor => Color.Parse("#FF5A4F45");

    // Control Background Hierarchy
    public override Color ThemeControlLowColor => Color.Parse("#FF211E1A");
    public override Color ThemeControlMidColor => Color.Parse("#FF2A2622");
    public override Color ThemeControlMidHighColor => Color.Parse("#FF33302A");
    public override Color ThemeControlHighColor => Color.Parse("#FF3D3A32");
    public override Color ThemeControlVeryHighColor => Color.Parse("#FF47433B");

    // Highlight/Hover States
    public override Color ThemeControlHighlightLowColor => Color.Parse("#FF242018");
    public override Color ThemeControlHighlightMidColor => Color.Parse("#FF2D291F");
    public override Color ThemeControlHighlightHighColor => Color.Parse("#FF363227");

    // Text Hierarchy
    public override Color ThemeForegroundLowColor => Color.Parse("#FFBAB5AE");

    // Texto deshabilitado (lo consume TabbedPage en Avalonia 12)
    public override Color ThemeForegroundMidColor => Color.Parse("#FFD4D0C9");
    public override Color ThemeForegroundHighColor => Color.Parse("#FFFAF8F5");

    // Accent Colors
    public override Color ThemeAccentColor => Color.Parse("#FFDB7A47");
    public override Color ThemeAccentLightColor => Color.Parse("#FFEDA975");
    public override Color ThemeAccentDarkColor => Color.Parse("#FFC56A3A");

    // State Colors
    public override Color HighlightColor => Color.Parse("#FFDB7A47");
    public override Color HighlightColor2 => Color.Parse("#FFC56A3A");
    public override Color HyperlinkColor => Color.Parse("#FFEDA975");
    public override Color HyperlinkVisitedColor => Color.Parse("#FFB8956F");

    // Status Colors
    public override Color ErrorColor => Color.Parse("#FFE67A5B");
    public override Color WarningColor => Color.Parse("#FFEDAA47");
    public override Color SuccessColor => Color.Parse("#FF8DB368");
    public override Color InfoColor => Color.Parse("#FF78A6D1");

    // Interactive States
    public override Color ButtonHoverColor => Color.Parse("#FF424038");
    public override Color ButtonPressedColor => Color.Parse("#FF36332B");
    public override Color ButtonDisabledColor => Color.Parse("#FF2A2622");
    public override Color InputHoverColor => Color.Parse("#FF383530");
    public override Color InputFocusColor => Color.Parse("#FF3D3A32");
    public override Color InputDisabledColor => Color.Parse("#FF252218");

    // Selection States
    public override Color SelectionColor => Color.Parse("#FFDB7A47");
    public override Color SelectionBackgroundColor => Color.Parse("#33DB7A47");
    public override Color SelectionHoverColor => Color.Parse("#66DB7A47");

    // ScrollBar Colors
    public override Color ScrollBarTrackColor => Color.Parse("#FF2A2622");
    public override Color ScrollBarThumbColor => Color.Parse("#FF47433B");
    public override Color ScrollBarThumbHoverColor => Color.Parse("#FF5A4F45");

    // Window Chrome Colors
    public override Color WindowChromeBackgroundColor => Color.Parse("#FF211E1A");
    public override Color WindowChromeForegroundColor => Color.Parse("#FFFAF8F5");

    // Menu Colors
    public override Color MenuBackgroundColor => Color.Parse("#FF211E1A");
    public override Color MenuBorderColor => Color.Parse("#FF3A342C");
    public override Color MenuItemHoverColor => Color.Parse("#FF2D291F");
    public override Color MenuSeparatorColor => Color.Parse("#FF3A342C");

    // Tooltip Colors
    public override Color TooltipBackgroundColor => Color.Parse("#FF2A2622");
    public override Color TooltipForegroundColor => Color.Parse("#FFFAF8F5");
    public override Color TooltipBorderColor => Color.Parse("#FF3A342C");

    // Calendar Colors
    public override Color CalendarBackgroundColor => Color.Parse("#FF211E1A");
    public override Color CalendarHeaderBackgroundColor => Color.Parse("#FF2A2622");
    public override Color CalendarButtonHoverColor => Color.Parse("#FF2D291F");
    public override Color CalendarButtonTodayColor => Color.Parse("#33DB7A47");
    public override Color CalendarButtonSelectedColor => Color.Parse("#FFDB7A47");

    // Notification Colors
    public override Color NotificationBackgroundColor => Color.Parse("#FF252218");
    public override Color NotificationInfoBackgroundColor => Color.Parse("#FF1F2426");
    public override Color NotificationSuccessBackgroundColor => Color.Parse("#FF222619");
    public override Color NotificationWarningBackgroundColor => Color.Parse("#FF2B2318");
    public override Color NotificationErrorBackgroundColor => Color.Parse("#FF2A1F1A");

    // Tab Colors
    public override Color TabBackgroundColor => Color.Parse("#FF211E1A");
    public override Color TabSelectedBackgroundColor => Color.Parse("#FF1A1612");
    public override Color TabHoverBackgroundColor => Color.Parse("#FF2D291F");
    public override Color TabBorderColor => Color.Parse("#FF3A342C");

    // DataGrid Colors
    public override Color DataGridHeaderBackgroundColor => Color.Parse("#FF2A2622");
    public override Color DataGridRowHoverColor => Color.Parse("#FF242018");
    public override Color DataGridRowSelectedColor => Color.Parse("#33DB7A47");
    public override Color DataGridBorderColor => Color.Parse("#FF3A342C");

    // Expander Colors
    public override Color ExpanderHeaderBackgroundColor => Color.Parse("#FF2A2622");
    public override Color ExpanderHeaderHoverColor => Color.Parse("#FF2D291F");
    public override Color ExpanderContentBackgroundColor => Color.Parse("#FF211E1A");

    // Flyout Colors
    public override Color FlyoutBackgroundColor => Color.Parse("#FF211E1A");
    public override Color FlyoutBorderColor => Color.Parse("#FF3A342C");

    // GLOBAL THEME COLORS (OVERRIDE DEFAULTS)
    public override Color ThemeAccentColor2 => Color.Parse("#B3DB7A47");
    public override Color ThemeAccentColor3 => Color.Parse("#66DB7A47");
    public override Color ThemeAccentColor4 => Color.Parse("#33DB7A47");
    public override Color HighlightForegroundColor => Color.Parse("#FFFFFFFF");
    public override Color ErrorLowColor => Color.Parse("#33E67A5B");
}

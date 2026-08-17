using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Controls;
using CdCSharp.Pangea.Theming.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// The theme selector is a control the toolkit ships, so its accessibility is the toolkit's to get
/// right rather than the application's to work around.
/// </summary>
/// <remarks>
/// Everything it shows is an emoji. Sighted users get a tooltip; a screen reader reads the
/// accessible name, and without one it announces a glyph or nothing at all.
/// </remarks>
public class ThemeSelectorAccessibilityTests
{
    private sealed class Services : IServiceProvider
    {
        private readonly ThemeService _themes = new();

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRelayCommandFactory)) return new RelayCommandFactory(null);
            if (serviceType == typeof(IThemeService)) return _themes;

            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(ILogger<>))
            {
                return Activator.CreateInstance(
                    typeof(NullLogger<>).MakeGenericType(serviceType.GetGenericArguments()[0]));
            }

            return null;
        }
    }

    private static (ThemeSelector Selector, ToggleButton Toggle, Window Window) Show()
    {
        ThemeSelector selector = new() { DataContext = new ThemeSelectorViewModel(new Services()) };

        Window window = new() { Content = selector, Width = 200, Height = 200 };
        window.Show();
        window.Activate();

        ToggleButton toggle = selector.GetVisualDescendants().OfType<ToggleButton>().First();

        return (selector, toggle, window);
    }

    [AvaloniaFact]
    public void TheSelectorCanBeReachedByKeyboard()
    {
        (_, ToggleButton toggle, Window window) = Show();

        Assert.True(toggle.Focusable);
        Assert.True(toggle.Focus());
        window.Close();
    }

    /// <summary>
    /// Without this a screen reader announces the emoji, which tells nobody anything.
    /// </summary>
    [AvaloniaFact]
    public void TheSelectorSaysWhatItIs()
    {
        (_, ToggleButton toggle, Window window) = Show();

        string? name = AutomationProperties.GetName(toggle);

        Assert.False(string.IsNullOrWhiteSpace(name), "The selector has no accessible name.");
        Assert.Contains("light", name, StringComparison.OrdinalIgnoreCase);
        window.Close();
    }

    /// <summary>The name is the current state, so it has to follow the state.</summary>
    [AvaloniaFact]
    public void TheNameFollowsTheVariant()
    {
        (ThemeSelector selector, ToggleButton toggle, Window window) = Show();

        string? before = AutomationProperties.GetName(toggle);

        ((ThemeSelectorViewModel)selector.DataContext!).IsDark = true;

        string? after = AutomationProperties.GetName(toggle);

        Assert.NotEqual(before, after);
        Assert.Contains("dark", after, StringComparison.OrdinalIgnoreCase);
        window.Close();
    }
}

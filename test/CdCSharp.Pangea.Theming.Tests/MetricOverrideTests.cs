using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Tests.Infrastructure;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// What the documentation tells an application to do about sizing: the metrics live in the theme,
/// so an application resource of the same name wins over them and every control that reads the
/// metric resizes at once.
/// </summary>
/// <remarks>
/// Application resources are consulted before application styles, and the theme is a style, which
/// is why the override lands without touching the control themes.
/// </remarks>
public class MetricOverrideTests
{
    [AvaloniaFact]
    public void AnApplicationResource_OverridesTheMetricForEveryControlThatReadsIt()
    {
        ThemeHarness.ApplyVariant(ThemeVariant.Default);

        double stock = (double)ThemeMetrics.Values["ComboBoxMinHeight"];
        double roomier = stock + 16;

        Application.Current!.Resources["ComboBoxMinHeight"] = roomier;

        try
        {
            ComboBox combo = new();
            Window window = new() { Width = 400, Height = 300, Content = combo };
            window.Show();
            window.Measure(new Size(400, 300));
            window.Arrange(new Rect(0, 0, 400, 300));

            Assert.True(combo.Bounds.Height >= roomier,
                $"ComboBox laid out at {combo.Bounds.Height}; the application override asked for at " +
                $"least {roomier}, so the control theme is reading a hardcoded size instead.");
        }
        finally
        {
            Application.Current.Resources.Remove("ComboBoxMinHeight");
        }
    }
}

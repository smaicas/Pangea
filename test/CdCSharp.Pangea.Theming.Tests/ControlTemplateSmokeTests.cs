using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Tests.Infrastructure;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// L3 - proves the control themes actually build a visual tree. A dictionary can compile and every
/// resource can resolve while a template still throws on the first layout pass; that only shows up
/// when something renders. Each themed control is instantiated, templated and laid out.
/// </summary>
public class ControlTemplateSmokeTests
{
    public static TheoryData<string> SmokeTestableControls()
    {
        TheoryData<string> data = new();
        foreach (string name in ThemedControls.SmokeTestableNames())
        {
            data.Add(name);
        }

        return data;
    }

    [AvaloniaTheory]
    [MemberData(nameof(SmokeTestableControls))]
    public void ThemedControl_AppliesTemplateAndLaysOut(string controlName)
    {
        ThemeHarness.ApplyVariant(ThemeVariant.Dark);

        Type type = ThemedControls.Resolve(controlName)!;
        Control control = (Control)Activator.CreateInstance(type)!;

        Window window = new() { Width = 400, Height = 300, Content = control };
        window.Show();

        control.ApplyTemplate();
        window.Measure(new Size(400, 300));
        window.Arrange(new Rect(0, 0, 400, 300));

        Assert.True(control.IsAttachedToVisualTree(),
            $"{controlName} never attached to the visual tree.");

        if (control is TemplatedControl)
        {
            Assert.True(control.GetVisualChildren().Any(),
                $"The control theme for {controlName} produced no visual children, so it renders as a blank box.");
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(SmokeTestableControls))]
    public void ThemedControl_SurvivesAThemeSwitch(string controlName)
    {
        Type type = ThemedControls.Resolve(controlName)!;
        Control control = (Control)Activator.CreateInstance(type)!;

        Window window = new() { Width = 400, Height = 300, Content = control };
        window.Show();

        foreach (ThemeVariant variant in ThemeHarness.Variants)
        {
            ThemeHarness.ApplyVariant(variant);
            window.Measure(new Size(400, 300));
            window.Arrange(new Rect(0, 0, 400, 300));
        }

        Assert.True(control.IsAttachedToVisualTree(),
            $"{controlName} fell out of the visual tree while switching theme.");
    }

    [Fact]
    public void SkipList_OnlyMentionsControlsTheThemeStillTargets()
    {
        IReadOnlyList<string> targets = ThemedControls.TargetTypeNames();

        List<string> stale = ThemedControls.NotSmokeTestable.Keys
            .Where(name => !targets.Contains(name))
            .ToList();

        Assert.True(stale.Count == 0,
            "These controls are excluded from the smoke tests but the theme no longer targets them, " +
            "so the exclusion is hiding nothing and should be removed: " + string.Join(", ", stale));
    }

    [Fact]
    public void Discovery_FindsTheControlsWeExpect()
    {
        IReadOnlyList<string> names = ThemedControls.SmokeTestableNames();

        // A guard against the discovery regex silently matching nothing and turning the whole
        // theory into a no-op that reports green.
        Assert.True(names.Count > 40, $"Only discovered {names.Count} themed controls; discovery is broken.");
        Assert.Contains("Button", names);
        Assert.Contains("ComboBox", names);
        Assert.Contains("TreeView", names);
    }

    [AvaloniaFact]
    public void InteractiveControls_AreLaidOutAtTheirMetricHeight()
    {
        ThemeHarness.ApplyVariant(ThemeVariant.Default);

        ComboBox combo = new();
        Button button = new();
        TextBox text = new();
        StackPanel panel = new() { Children = { combo, button, text } };

        Window window = new() { Width = 400, Height = 300, Content = panel };
        window.Show();
        window.Measure(new Size(400, 300));
        window.Arrange(new Rect(0, 0, 400, 300));

        // The metrics are the whole point of ThemeMetrics: if a control theme goes back to a
        // hardcoded size, the touch target shrinks and this catches it.
        AssertAtLeast("ComboBox", "ComboBoxMinHeight", combo);
        AssertAtLeast("Button", "ButtonMinHeight", button);
        AssertAtLeast("TextBox", "TextBoxMinHeight", text);
    }

    private static void AssertAtLeast(string controlName, string metricKey, Control control)
    {
        double floor = (double)ThemeMetrics.Values[metricKey];

        Assert.True(control.Bounds.Height >= floor,
            $"{controlName} laid out at {control.Bounds.Height} but {metricKey} asks for at least {floor}, " +
            "so the control theme is not reading the metric.");
    }
}

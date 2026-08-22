using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// The mobile templates keep their component styles in a file of their own, added to App.axaml as a
/// type rather than through an <c>avares://</c> URI.
/// </summary>
/// <remarks>
/// The type is what makes a rename safe - a URI carries the assembly name in a string and stops
/// resolving silently when the project is renamed - but it moves the failure to runtime: a
/// <c>Styles</c> subclass whose XAML did not load is an empty one, and an application with an empty
/// style file starts perfectly and simply looks wrong. This is what notices.
/// </remarks>
public class ComponentStylesTests
{
    public static TheoryData<string> Templates() => new("Mobile", "Supabase");

    private static Styles ComponentsFor(string template) => template switch
    {
        "Mobile" => new PangeaMobileApp.Themes.Components(),
        _ => new PangeaSupabaseApp.Themes.Components()
    };

    [AvaloniaTheory]
    [MemberData(nameof(Templates))]
    public void TheStyleFile_LoadsItsRules(string template) => Assert.NotEmpty(ComponentsFor(template));

    [AvaloniaTheory]
    [MemberData(nameof(Templates))]
    public void ThePrimaryButtonStyle_ReachesAButtonThatAsksForIt(string template)
    {
        Button button = new() { Classes = { "primary" } };
        Window window = new() { Content = button };

        window.Styles.Add(ComponentsFor(template));
        window.Show();

        button.ApplyTemplate();

        // The class is the whole contract between a view and the design system: a selector that no
        // longer matches leaves the button styled by the theme alone, which is not a failure
        // anything reports.
        Assert.Equal(FontWeight.SemiBold, button.FontWeight);
    }
}

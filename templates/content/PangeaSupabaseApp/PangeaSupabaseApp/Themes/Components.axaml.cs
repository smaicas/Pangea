using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PangeaSupabaseApp.Themes;

/// <summary>
/// The application's component styles, as a type rather than a resource URI.
/// </summary>
/// <remarks>
/// App.axaml adds this with <c>&lt;themes:Components /&gt;</c>. The alternative, a StyleInclude
/// pointing at <c>avares://PangeaSupabaseApp/Themes/Components.axaml</c>, writes the assembly name
/// into a string: rename the project and the styles stop being applied, with nothing at build time
/// to say so. A type reference is checked by the compiler and follows a rename on its own.
/// </remarks>
public partial class Components : Styles
{
    public Components() => AvaloniaXamlLoader.Load(this);
}

using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace CdCSharp.Pangea.Theming;

/// <summary>
/// The toolkit's control themes and palettes, added to <c>Application.Styles</c>.
/// </summary>
/// <remarks>Nothing but styles: the wiring lives in <see cref="ThemingFeature"/>.</remarks>
public class PangeaUI : Styles
{
    public PangeaUI() => AvaloniaXamlLoader.Load(this);
}

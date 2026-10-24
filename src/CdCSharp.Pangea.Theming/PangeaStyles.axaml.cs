using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace CdCSharp.Pangea.Theming;

/// <summary>
/// Opt-in classes over the palette - <c>card</c>, <c>hint</c>, <c>section</c>, <c>accent</c>,
/// <c>subtle</c>, <c>nav</c>, <c>tool</c>, <c>banner</c> and the rest - and the <c>Icon.*</c>
/// geometries, so applications in one host look like one family.
/// </summary>
/// <remarks>
/// Not added by the toolkit, unlike <see cref="PangeaUI"/>: an application that styles its own
/// <c>card</c> keeps doing so. Put it first in <c>Application.Styles</c>, so the application's own
/// styles, declared after it, win where they overlap.
/// </remarks>
public class PangeaStyles : Styles
{
    public PangeaStyles() => AvaloniaXamlLoader.Load(this);
}

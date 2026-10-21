using Avalonia.Media;

namespace CdCSharp.Pangea.Theming.Palettes;

/// <summary>
/// A palette given as data: colours by resource key, rather than a class overriding properties.
/// </summary>
/// <remarks>
/// <para>
/// What <see cref="PangeaPalette.ToColours"/> produces, read back. That round trip is what lets a
/// palette cross a process boundary: a shell that imposes its look on the applications beside it
/// sends the colours, and each application builds a theme out of them with this.
/// </para>
/// <para>
/// A key the dictionary does not have keeps the default palette's colour, so a palette written
/// by an older version of the toolkit - one that did not know a colour added since - still
/// produces every resource the control themes ask for.
/// </para>
/// </remarks>
/// <param name="colours">The colours, by the resource key they produce.</param>
/// <param name="pickerHighlightOpacity">Opacity of the date and time picker highlight fill.</param>
public sealed class DictionaryPalette(IReadOnlyDictionary<string, Color> colours, double pickerHighlightOpacity = 0.2d)
    : PangeaPalette
{
    private readonly IReadOnlyDictionary<string, Color> _colours =
        colours ?? throw new ArgumentNullException(nameof(colours));

    public override double PickerHighlightOpacity => pickerHighlightOpacity;

    public override IReadOnlyDictionary<string, Color> ToColours()
    {
        Dictionary<string, Color> merged = new(base.ToColours(), StringComparer.Ordinal);

        foreach ((string key, Color colour) in _colours)
        {
            merged[key] = colour;
        }

        return merged;
    }
}

using CdCSharp.Pangea.Localization.Abstractions;
using System.Globalization;

namespace CdCSharp.Pangea.Testing.Fakes;

/// <summary>
/// Localized strings from a dictionary rather than from compiled resources.
/// </summary>
/// <remarks>
/// Behaves like the real service where it matters: an unknown key comes back as itself, changing
/// culture raises <see cref="CultureChanged"/>, and a culture outside the supported set is refused.
/// What it drops is the part a test cannot easily arrange - satellite assemblies, resource
/// managers, and a build that produces them.
/// </remarks>
public sealed class DictionaryLocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _strings;

    /// <param name="stringsByCulture">
    /// The strings for each culture, keyed by culture name. The first entry is the culture it
    /// starts on.
    /// </param>
    public DictionaryLocalizationService(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> stringsByCulture)
    {
        ArgumentNullException.ThrowIfNull(stringsByCulture);

        if (stringsByCulture.Count == 0)
        {
            throw new ArgumentException("At least one culture is needed.", nameof(stringsByCulture));
        }

        _strings = stringsByCulture.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);

        CurrentCulture = CultureInfo.GetCultureInfo(stringsByCulture.Keys.First());
    }

    /// <summary>One culture holding <paramref name="strings"/>, for a test that never switches.</summary>
    public static DictionaryLocalizationService For(string culture, IReadOnlyDictionary<string, string> strings) =>
        new(new Dictionary<string, IReadOnlyDictionary<string, string>> { [culture] = strings });

    public CultureInfo CurrentCulture { get; private set; }

    public IEnumerable<CultureInfo> SupportedCultures => _strings.Keys.Select(CultureInfo.GetCultureInfo);

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;

    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        return _strings.TryGetValue(CurrentCulture.Name, out Dictionary<string, string>? strings) &&
               strings.TryGetValue(key, out string? value) && value.Length > 0
            ? value
            : key;
    }

    public void SetCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        if (!_strings.ContainsKey(cultureName))
        {
            throw new NotSupportedException(
                $"Culture '{cultureName}' is not supported. Supported cultures: {string.Join(", ", _strings.Keys)}");
        }

        CultureInfo previous = CurrentCulture;
        CultureInfo next = CultureInfo.GetCultureInfo(cultureName);

        if (string.Equals(previous.Name, next.Name, StringComparison.OrdinalIgnoreCase)) return;

        CurrentCulture = next;
        CultureChanged?.Invoke(this, new CultureChangedEventArgs(previous, next));
    }
}

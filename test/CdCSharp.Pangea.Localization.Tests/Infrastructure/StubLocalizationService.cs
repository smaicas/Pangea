using CdCSharp.Pangea.Localization.Abstractions;
using System.Globalization;

namespace CdCSharp.Pangea.Localization.Tests.Infrastructure;

/// <summary>
/// A localization service backed by a dictionary, so a test can say what the supported cultures
/// are without building resource assemblies for them.
/// </summary>
internal sealed class StubLocalizationService : ILocalizationService
{
    private readonly List<CultureInfo> _supported;
    private readonly Dictionary<string, Dictionary<string, string>> _strings;

    public StubLocalizationService(params string[] supported)
        : this(supported.ToDictionary(culture => culture, _ => new Dictionary<string, string>())) { }

    public StubLocalizationService(Dictionary<string, Dictionary<string, string>> strings)
    {
        _strings = new Dictionary<string, Dictionary<string, string>>(strings, StringComparer.OrdinalIgnoreCase);
        _supported = _strings.Keys.Select(CultureInfo.GetCultureInfo).ToList();
        CurrentCulture = _supported[0];
    }

    public CultureInfo CurrentCulture { get; private set; }

    public IEnumerable<CultureInfo> SupportedCultures => _supported;

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;

    public string GetString(string key) =>
        _strings.TryGetValue(CurrentCulture.Name, out Dictionary<string, string>? strings) &&
        strings.TryGetValue(key, out string? value)
            ? value
            : key;

    public void SetCulture(string cultureName)
    {
        if (!_strings.ContainsKey(cultureName))
        {
            throw new NotSupportedException($"Culture '{cultureName}' is not supported.");
        }

        CultureInfo previous = CurrentCulture;
        CultureInfo next = CultureInfo.GetCultureInfo(cultureName);

        if (string.Equals(previous.Name, next.Name, StringComparison.OrdinalIgnoreCase)) return;

        CurrentCulture = next;
        CultureChanged?.Invoke(this, new CultureChangedEventArgs(previous, next));
    }
}

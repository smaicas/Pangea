using System.Globalization;

namespace CdCSharp.Pangea.Localization.Resources;

/// <summary>
/// Presentation helpers for culture pickers.
/// </summary>
public static class CultureHelper
{
    private const string UnknownFlag = "🌐";

    /// <remarks>Keyed with the canonical casing, matched case-insensitively.</remarks>
    private static readonly Dictionary<string, string> FlagsByCulture = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "🇺🇸", ["en-US"] = "🇺🇸", ["en-GB"] = "🇬🇧",
        ["es"] = "🇪🇸", ["es-ES"] = "🇪🇸", ["es-MX"] = "🇲🇽",
        ["fr"] = "🇫🇷", ["fr-FR"] = "🇫🇷", ["fr-CA"] = "🇨🇦",
        ["de"] = "🇩🇪", ["de-DE"] = "🇩🇪", ["de-AT"] = "🇦🇹",
        ["it"] = "🇮🇹", ["it-IT"] = "🇮🇹",
        ["pt"] = "🇵🇹", ["pt-PT"] = "🇵🇹", ["pt-BR"] = "🇧🇷",
        ["ja"] = "🇯🇵", ["ja-JP"] = "🇯🇵",
        ["zh"] = "🇨🇳", ["zh-CN"] = "🇨🇳", ["zh-TW"] = "🇹🇼"
    };

    /// <summary>
    /// Flag for a culture code, falling back to the neutral language before giving up, so an
    /// unlisted region such as "es-AR" still shows a Spanish flag rather than the globe.
    /// </summary>
    public static string GetFlagEmoji(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return UnknownFlag;

        if (FlagsByCulture.TryGetValue(cultureCode, out string? flag)) return flag;

        int separator = cultureCode.IndexOf('-');
        if (separator > 0 && FlagsByCulture.TryGetValue(cultureCode[..separator], out string? neutralFlag))
        {
            return neutralFlag;
        }

        return UnknownFlag;
    }

    public static string GetFlagEmoji(CultureInfo culture) => GetFlagEmoji(culture.Name);

    public static bool IsKnownCulture(string cultureCode) =>
        !string.IsNullOrWhiteSpace(cultureCode) && FlagsByCulture.ContainsKey(cultureCode);

    public static string GetDisplayName(CultureInfo culture) => $"{GetFlagEmoji(culture)} {culture.DisplayName}";
}

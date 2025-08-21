namespace CdCSharp.Pangea.Localization.Resources;

using System.Globalization;

public static class CultureHelper
{
    private static readonly Dictionary<string, string> CultureToFlagMap = new()
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

    public static string GetFlagEmoji(string cultureCode)
    {
        return CultureToFlagMap.TryGetValue(cultureCode.ToLowerInvariant(), out string? flag) 
            ? flag 
            : "🌐";
    }

    public static string GetFlagEmoji(CultureInfo culture) => GetFlagEmoji(culture.Name);

    public static bool IsVariant(CultureInfo culture) => culture.Name.Contains('-');

    public static bool IsKnownCulture(string cultureCode) =>
        CultureToFlagMap.ContainsKey(cultureCode.ToLowerInvariant());

    public static string GetDisplayName(CultureInfo culture)
    {
        string flag = GetFlagEmoji(culture);
        return $"{flag} {culture.DisplayName}";
    }
}
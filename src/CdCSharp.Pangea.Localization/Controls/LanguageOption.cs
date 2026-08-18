using System.Globalization;

namespace CdCSharp.Pangea.Localization.Controls;

/// <summary>
/// One language a user can pick, named the way a speaker of it would recognise.
/// </summary>
/// <remarks>
/// The native name, not the current culture's name for it: someone looking for Spanish in a
/// window that is currently in English is looking for "Español", not "Spanish". That is the one
/// label in a language picker that must never be localized.
/// </remarks>
public sealed class LanguageOption
{
    public LanguageOption(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        Culture = culture;
        DisplayName = Describe(culture);
    }

    public CultureInfo Culture { get; }

    /// <summary>The culture name, as <c>SetCulture</c> takes it.</summary>
    public string Name => Culture.Name;

    /// <summary>What the picker shows.</summary>
    public string DisplayName { get; }

    public override string ToString() => DisplayName;

    /// <summary>
    /// The native name with its first letter capitalised, because most cultures report it in lower
    /// case and a list of lower-case entries reads as an oversight.
    /// </summary>
    private static string Describe(CultureInfo culture)
    {
        string native = culture.NativeName;

        if (native.Length == 0) return culture.Name;

        return char.ToUpper(native[0], culture) + native[1..];
    }
}

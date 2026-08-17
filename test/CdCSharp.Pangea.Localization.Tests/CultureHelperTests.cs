using CdCSharp.Pangea.Localization.Resources;
using System.Globalization;

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// The lookup lowercased the culture code before searching a map keyed "en-US", so every regional
/// culture fell through to the globe and only neutral codes ever matched.
/// </summary>
public class CultureHelperTests
{
    [Theory]
    [InlineData("en-US", "🇺🇸")]
    [InlineData("en-GB", "🇬🇧")]
    [InlineData("es-MX", "🇲🇽")]
    [InlineData("pt-BR", "🇧🇷")]
    public void RegionalCultures_GetTheirOwnFlag(string cultureCode, string expected) =>
        Assert.Equal(expected, CultureHelper.GetFlagEmoji(cultureCode));

    [Theory]
    [InlineData("EN-US")]
    [InlineData("en-us")]
    public void LookupIsCaseInsensitive(string cultureCode) =>
        Assert.Equal("🇺🇸", CultureHelper.GetFlagEmoji(cultureCode));

    [Fact]
    public void UnlistedRegion_FallsBackToItsLanguage() =>
        // "es-AR" is not in the map, but Spanish is.
        Assert.Equal("🇪🇸", CultureHelper.GetFlagEmoji("es-AR"));

    [Theory]
    [InlineData("xx-XX")]
    [InlineData("")]
    [InlineData("   ")]
    public void UnknownCulture_FallsBackToTheGlobe(string cultureCode) =>
        Assert.Equal("🌐", CultureHelper.GetFlagEmoji(cultureCode));

    [Fact]
    public void IsKnownCulture_MatchesRegardlessOfCasing()
    {
        Assert.True(CultureHelper.IsKnownCulture("es-ES"));
        Assert.True(CultureHelper.IsKnownCulture("ES-es"));
        Assert.False(CultureHelper.IsKnownCulture("xx-XX"));
    }

    [Fact]
    public void DisplayName_CombinesFlagAndCultureName()
    {
        string displayName = CultureHelper.GetDisplayName(CultureInfo.GetCultureInfo("es-ES"));

        Assert.StartsWith("🇪🇸 ", displayName);
        Assert.Contains(CultureInfo.GetCultureInfo("es-ES").DisplayName, displayName);
    }
}

using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Localization.Services;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// Starting in the language the device is set to.
/// </summary>
/// <remarks>
/// A phone reports its locale in whatever form it likes: Android commonly gives a bare "en" or
/// "es", iOS gives "en-GB" on a British device. Comparing that against an application's "en-US"
/// meant auto-detection never fired on a device at all - every user got the default language
/// however their phone was set, which is the kind of failure nobody reports because it looks like a
/// decision somebody made.
/// </remarks>
[Collection(nameof(CultureCollection))]
public class CultureDetectionTests
{
    private static LocalizationService Detecting(string deviceCulture, params string[] supported)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(deviceCulture);

            return new LocalizationService(Options.Create(new LocalizationOptions
            {
                DefaultCulture = "es-ES",
                AutoDetectCulture = true,
                SupportedCultures = supported.Length > 0 ? [.. supported] : ["es-ES", "en-US"]
            }));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void AnExactMatchIsUsed() => Assert.Equal("en-US", Detecting("en-US").CurrentCulture.Name);

    /// <summary>The case that made this necessary: Android hands over a language and no region.</summary>
    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("es", "es-ES")]
    public void ALanguageWithNoRegionFindsTheRegionTheApplicationOffers(string device, string expected) =>
        Assert.Equal(expected, Detecting(device).CurrentCulture.Name);

    /// <summary>
    /// A British phone gets the American strings, which is the right trade against no English at
    /// all. The region comes from what the application has a resource file for.
    /// </summary>
    [Theory]
    [InlineData("en-GB", "en-US")]
    [InlineData("es-MX", "es-ES")]
    public void ADifferentRegionOfTheSameLanguageStillMatches(string device, string expected) =>
        Assert.Equal(expected, Detecting(device).CurrentCulture.Name);

    [Fact]
    public void ALanguageTheApplicationDoesNotHaveFallsBackToTheDefault() =>
        Assert.Equal("es-ES", Detecting("ja-JP").CurrentCulture.Name);

    /// <summary>
    /// Detection settles both cultures, not just the one the strings come from. A screen in Spanish
    /// showing "€22.22" is the mismatch this guards against.
    /// </summary>
    [Fact]
    public void TheDetectedCultureFormatsNumbersAsWellAsChoosingStrings()
    {
        // Asserted before the helper puts the thread's culture back, because what is being checked
        // is the state the service leaves behind.
        CultureInfo previous = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es");

            _ = new LocalizationService(Options.Create(new LocalizationOptions
            {
                DefaultCulture = "en-US",
                AutoDetectCulture = true,
                SupportedCultures = ["es-ES", "en-US"]
            }));

            Assert.Equal("es-ES", CultureInfo.CurrentCulture.Name);
            Assert.Equal("es-ES", CultureInfo.CurrentUICulture.Name);
            Assert.Equal("22,80", 22.8m.ToString("0.00", CultureInfo.CurrentCulture));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            CultureInfo.CurrentCulture = previous;
        }
    }
}

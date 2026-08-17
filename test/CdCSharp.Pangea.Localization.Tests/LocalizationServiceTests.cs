using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Localization.Services;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// String lookup and culture switching, against real embedded resources and a real satellite
/// assembly rather than a stubbed resource manager.
/// </summary>
[Collection(nameof(CultureCollection))]
public class LocalizationServiceTests
{
    private static LocalizationService Create(
        bool withResources = true,
        bool autoDetect = false,
        string defaultCulture = "en-US",
        params string[] supported)
    {
        LocalizationOptions options = new()
        {
            DefaultCulture = defaultCulture,
            AutoDetectCulture = autoDetect,
            SupportedCultures = supported.Length > 0 ? [.. supported] : ["en-US", "es-ES"]
        };

        if (withResources) options.ResourceAssemblies.Add(typeof(Resources.TestStrings).Assembly);

        return new LocalizationService(Options.Create(options));
    }

    [Fact]
    public void ResolvesAStringFromTheDeclaredResourceAssembly()
    {
        LocalizationService service = Create();

        Assert.Equal("Hello", service.GetString("Greeting"));
    }

    [Fact]
    public void ResolvesTheTranslationAfterSwitchingCulture()
    {
        LocalizationService service = Create();

        service.SetCulture("es-ES");

        Assert.Equal("Hola", service.GetString("Greeting"));
    }

    [Fact]
    public void FallsBackToTheNeutralResourceWhenTheTranslationIsMissing()
    {
        LocalizationService service = Create();

        service.SetCulture("es-ES");

        Assert.Equal("Neutral only", service.GetString("OnlyInNeutral"));
    }

    [Fact]
    public void UnknownKey_IsReturnedAsIs()
    {
        // A visible key beats a blank label when a translation is missing.
        LocalizationService service = Create();

        Assert.Equal("NoSuchKey", service.GetString("NoSuchKey"));
    }

    [Fact]
    public void WithoutResourceAssemblies_EveryKeyComesBackUntranslated()
    {
        LocalizationService service = Create(withResources: false);

        Assert.Equal("Greeting", service.GetString("Greeting"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EmptyKey_IsReturnedUnchanged(string? key)
    {
        LocalizationService service = Create();

        Assert.Equal(key, service.GetString(key!));
    }

    [Fact]
    public void SetCulture_AppliesToTheWholeApplicationNotJustTheCallingThread()
    {
        LocalizationService service = Create();

        service.SetCulture("es-ES");

        // Threads started afterwards must see the change too.
        string cultureOnAnotherThread = string.Empty;
        Thread worker = new(() => cultureOnAnotherThread = CultureInfo.CurrentCulture.Name);
        worker.Start();
        worker.Join();

        Assert.Equal("es-ES", cultureOnAnotherThread);
        Assert.Equal("es-ES", CultureInfo.CurrentCulture.Name);
        Assert.Equal("es-ES", service.CurrentCulture.Name);
    }

    [Fact]
    public void SetCulture_RaisesCultureChangedWithBothCultures()
    {
        LocalizationService service = Create();
        CultureChangedEventArgs? raised = null;
        service.CultureChanged += (_, e) => raised = e;

        service.SetCulture("es-ES");

        Assert.NotNull(raised);
        Assert.Equal("en-US", raised!.PreviousCulture.Name);
        Assert.Equal("es-ES", raised.CurrentCulture.Name);
    }

    [Fact]
    public void SetCulture_ToTheCurrentCulture_RaisesNothing()
    {
        LocalizationService service = Create();
        int raised = 0;
        service.CultureChanged += (_, _) => raised++;

        service.SetCulture("en-US");

        Assert.Equal(0, raised);
    }

    [Fact]
    public void SetCulture_RejectsUnsupportedCultures()
    {
        LocalizationService service = Create();

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => service.SetCulture("fr-FR"));

        Assert.Contains("es-ES", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetCulture_RejectsBlankNames(string cultureName)
    {
        LocalizationService service = Create();

        Assert.Throws<ArgumentException>(() => service.SetCulture(cultureName));
    }

    [Fact]
    public void StartsOnTheDefaultCultureWhenAutoDetectionIsOff()
    {
        LocalizationService service = Create(autoDetect: false, defaultCulture: "es-ES");

        Assert.Equal("es-ES", service.CurrentCulture.Name);
    }

    [Fact]
    public void SupportedCultures_ReflectsTheConfiguredList()
    {
        LocalizationService service = Create(supported: ["en-US", "es-ES", "fr-FR"]);

        Assert.Equal(["en-US", "es-ES", "fr-FR"], service.SupportedCultures.Select(c => c.Name));
    }
}

/// <summary>
/// The culture is process-wide state, so these tests must not run alongside each other.
/// </summary>
[CollectionDefinition(nameof(CultureCollection), DisableParallelization = true)]
public class CultureCollection;

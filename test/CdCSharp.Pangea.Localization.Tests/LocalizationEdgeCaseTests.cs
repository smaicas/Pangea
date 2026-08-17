using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Localization.Services;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// Culture selection at its edges: how a name is written, how close a match has to be, and what a
/// listener sees when the culture moves.
/// </summary>
/// <remarks>
/// Nothing here was wrong when it was measured. It was simply unpinned, and every one of these is a
/// decision an application depends on - a stricter or looser match changes which strings a user
/// sees.
/// </remarks>
[Collection(nameof(CultureCollection))]
public class LocalizationEdgeCaseTests
{
    private static LocalizationService Create(params string[] supported)
    {
        LocalizationOptions options = new()
        {
            DefaultCulture = "en-US",
            AutoDetectCulture = false,
            SupportedCultures = supported.Length > 0 ? [.. supported] : ["en-US", "es-ES"]
        };

        options.ResourceAssemblies.Add(typeof(Resources.TestStrings).Assembly);

        return new LocalizationService(Options.Create(options));
    }

    /// <summary>Culture names are not case sensitive anywhere else; they are not here either.</summary>
    [Theory]
    [InlineData("es-ES")]
    [InlineData("ES-es")]
    [InlineData("es-es")]
    public void ACultureNameIsMatchedRegardlessOfCasing(string requested)
    {
        LocalizationService service = Create();

        service.SetCulture(requested);

        Assert.Equal("es-ES", service.CurrentCulture.Name);
        Assert.Equal("Hola", service.GetString("Greeting"));
    }

    /// <summary>
    /// Supporting es-ES is not supporting every Spanish. The match is exact, and the refusal names
    /// what is on offer so the application can widen its list deliberately.
    /// </summary>
    [Theory]
    [InlineData("es-MX")]
    [InlineData("es")]
    public void ACultureThatIsMerelyRelatedToASupportedOne_IsRefused(string requested)
    {
        LocalizationService service = Create("en-US", "es-ES");

        NotSupportedException error =
            Assert.Throws<NotSupportedException>(() => service.SetCulture(requested));

        Assert.Contains(requested, error.Message, StringComparison.Ordinal);
        Assert.Contains("es-ES", error.Message, StringComparison.Ordinal);
        Assert.Equal("en-US", service.CurrentCulture.Name);
    }

    /// <summary>
    /// A handler that reads the service rather than the event arguments must see the same thing.
    /// </summary>
    [Fact]
    public void WhenTheEventArrives_TheServiceAlreadyReportsTheNewCulture()
    {
        LocalizationService service = Create();

        string? reportedByService = null;
        string? reportedByEvent = null;
        string? previous = null;

        service.CultureChanged += (_, e) =>
        {
            previous = e.PreviousCulture.Name;
            reportedByEvent = e.CurrentCulture.Name;
            reportedByService = service.CurrentCulture.Name;
        };

        service.SetCulture("es-ES");

        Assert.Equal("en-US", previous);
        Assert.Equal("es-ES", reportedByEvent);
        Assert.Equal("es-ES", reportedByService);
    }

    [Fact]
    public void SwitchingAwayAndBack_ReturnsTheOriginalStrings()
    {
        LocalizationService service = Create();

        Assert.Equal("Hello", service.GetString("Greeting"));

        service.SetCulture("es-ES");
        Assert.Equal("Hola", service.GetString("Greeting"));

        service.SetCulture("en-US");
        Assert.Equal("Hello", service.GetString("Greeting"));
    }

    [Fact]
    public void BeforeAnythingIsAskedOfIt_TheServiceIsOnTheConfiguredDefault() =>
        Assert.Equal("en-US", Create().CurrentCulture.Name);

    [Fact]
    public void SupportedCultures_AreTheOnesConfigured()
    {
        LocalizationService service = Create("en-US", "es-ES", "fr-FR");

        Assert.Equal(
            "en-US,es-ES,fr-FR",
            string.Join(",", service.SupportedCultures.Select(culture => culture.Name)));
    }

    /// <summary>
    /// A culture name the operating system does not know is still accepted when the application
    /// listed it: the list is the application's statement of intent, not a guess to second-guess.
    /// Lookups then fall back to the neutral resources.
    /// </summary>
    [Fact]
    public void ACultureTheSystemDoesNotKnow_IsAcceptedWhenItWasConfigured()
    {
        LocalizationService service = Create("en-US", "xx-YY");

        service.SetCulture("xx-YY");

        Assert.Equal("xx-YY", service.CurrentCulture.Name);
        Assert.Equal("Hello", service.GetString("Greeting"));
    }
}

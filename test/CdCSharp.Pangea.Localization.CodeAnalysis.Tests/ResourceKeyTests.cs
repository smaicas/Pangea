using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Localization.CodeAnalysis.Tests;

/// <summary>
/// PGL001: a key that names nothing.
/// </summary>
/// <remarks>
/// The failure being guarded is silent by design - <c>GetString</c> returns the key so the screen
/// stays readable - which is why nobody notices it until a user reads <c>Home_Title</c> off the
/// window.
/// </remarks>
public class ResourceKeyTests
{
    private const string Screen = """
        using CdCSharp.Pangea.Localization.Abstractions;

        namespace Sample;

        public class Screen
        {
            private readonly ILocalizationService _localization;

            public Screen(ILocalizationService localization) => _localization = localization;

            public string Title => _localization.GetString("Home_Title");
        }
        """;

    [Fact]
    public void AKeyThatIsDefined_IsNotReported()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL001", Screen, AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Title"));

        Assert.Empty(reported);
    }

    [Fact]
    public void AKeyThatIsInNoResourceFile_IsReportedWithTheKeyInTheMessage()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL001", Screen, AnalyzerTestHelper.Resx("Resources/Strings.resx", "Something_Else"));

        Diagnostic diagnostic = Assert.Single(reported);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Home_Title", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A key defined only in a translation still resolves for that culture, so it is not missing.
    /// </summary>
    [Fact]
    public void AKeyThatOnlyATranslationDefines_IsNotReportedAsMissing()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL001", Screen, AnalyzerTestHelper.Resx("Resources/Strings.es.resx", "Home_Title"));

        Assert.Empty(reported);
    }

    /// <summary>
    /// With no resource files the analyzer knows nothing, and saying so about every key in the
    /// project would be worse than saying nothing at all.
    /// </summary>
    [Fact]
    public void WithNoResourceFilesAtAll_NothingIsReported()
    {
        Assert.Empty(AnalyzerTestHelper.Run(Screen));
    }

    [Fact]
    public void AKeyThatIsNotConstant_IsLeftAlone()
    {
        const string source = """
            using CdCSharp.Pangea.Localization.Abstractions;

            namespace Sample;

            public class Screen
            {
                private readonly ILocalizationService _localization;

                public Screen(ILocalizationService localization) => _localization = localization;

                public string For(string suffix) => _localization.GetString("Home_" + suffix);
            }
            """;

        Assert.Empty(AnalyzerTestHelper.Run(
            "PGL001", source, AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Title")));
    }

    /// <summary>
    /// A concrete implementation carries the attribute from the interface member it implements,
    /// so calling the class rather than the interface is checked the same way.
    /// </summary>
    [Fact]
    public void AKeyPassedToAnImplementation_IsCheckedThroughTheInterface()
    {
        const string source = """
            using CdCSharp.Pangea.Localization.Abstractions;
            using System;
            using System.Collections.Generic;
            using System.Globalization;

            namespace Sample;

            public class Fake : ILocalizationService
            {
                public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
                public IEnumerable<CultureInfo> SupportedCultures => Array.Empty<CultureInfo>();
                public string GetString(string key) => key;
                public void SetCulture(string cultureName) { }
                public event EventHandler<CultureChangedEventArgs>? CultureChanged;
            }

            public class Screen
            {
                public string Title => new Fake().GetString("Home_Title");
            }
            """;

        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL001", source, AnalyzerTestHelper.Resx("Resources/Strings.resx", "Something_Else"));

        Assert.Single(reported);
    }

    /// <summary>
    /// The point of the attribute being public: an application's own wrapper is checked too, and
    /// the indexer is the shape the shell template uses.
    /// </summary>
    [Fact]
    public void AKeyPassedToTheApplicationsOwnWrapper_IsChecked()
    {
        const string source = """
            using CdCSharp.Pangea.Localization.Abstractions;

            namespace Sample;

            public sealed class Strings
            {
                private readonly ILocalizationService _localization;

                public Strings(ILocalizationService localization) => _localization = localization;

                public string this[[LocalizationKey] string key] => _localization.GetString(key);
            }

            public class Screen
            {
                private readonly Strings _strings;

                public Screen(Strings strings) => _strings = strings;

                public string Title => _strings["Home_Title"];
            }
            """;

        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL001", source, AnalyzerTestHelper.Resx("Resources/Strings.resx", "Something_Else"));

        Diagnostic diagnostic = Assert.Single(reported);
        Assert.Contains("Home_Title", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>An unmarked parameter is an ordinary string, however much it looks like a key.</summary>
    [Fact]
    public void AStringPassedToSomethingElse_IsNotAKey()
    {
        const string source = """
            namespace Sample;

            public class Screen
            {
                public string Title => Describe("Home_Title");

                private static string Describe(string text) => text;
            }
            """;

        Assert.Empty(AnalyzerTestHelper.Run(
            "PGL001", source, AnalyzerTestHelper.Resx("Resources/Strings.resx", "Something_Else")));
    }
}

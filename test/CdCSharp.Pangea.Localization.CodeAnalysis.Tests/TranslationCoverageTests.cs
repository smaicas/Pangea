using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Localization.CodeAnalysis.Tests;

/// <summary>
/// PGL002: a key the neutral file defines and a translation does not.
/// </summary>
/// <remarks>
/// This one is about the resource files alone, so it is reported whether or not any code reads the
/// key. Falling back to the neutral text is the right runtime behaviour and the reason an
/// untranslated string can ship unnoticed: the application looks finished in every language.
/// </remarks>
public class TranslationCoverageTests
{
    private const string Empty = """
        namespace Sample;

        public class Nothing;
        """;

    [Fact]
    public void AKeyMissingFromATranslation_IsReportedWithTheFileThatLacksIt()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL002",
            Empty,
            AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Title", "Home_Subtitle"),
            AnalyzerTestHelper.Resx("Resources/Strings.es.resx", "Home_Title"));

        Diagnostic diagnostic = Assert.Single(reported);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Home_Subtitle", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Strings.es.resx", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryCultureThatLacksTheKey_IsNamed()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL002",
            Empty,
            AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Title"),
            AnalyzerTestHelper.Resx("Resources/Strings.es.resx"),
            AnalyzerTestHelper.Resx("Resources/Strings.fr.resx"));

        Diagnostic diagnostic = Assert.Single(reported);

        Assert.Contains("Strings.es.resx", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Strings.fr.resx", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void AFullyTranslatedKey_IsNotReported()
    {
        Assert.Empty(AnalyzerTestHelper.Run(
            "PGL002",
            Empty,
            AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Title"),
            AnalyzerTestHelper.Resx("Resources/Strings.es.resx", "Home_Title")));
    }

    /// <summary>
    /// A project that ships one language has nothing to be missing from, and reporting every key
    /// in it would make the rule unusable for the projects that need it least.
    /// </summary>
    [Fact]
    public void WithNoTranslationsAtAll_NothingIsReported()
    {
        Assert.Empty(AnalyzerTestHelper.Run(
            "PGL002", Empty, AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Title")));
    }

    /// <summary>
    /// Resource files are grouped by name and directory, so two unrelated sets do not appear to be
    /// translations of each other.
    /// </summary>
    [Fact]
    public void TwoUnrelatedResourceSets_AreNotComparedToEachOther()
    {
        Assert.Empty(AnalyzerTestHelper.Run(
            "PGL002",
            Empty,
            AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Title"),
            AnalyzerTestHelper.Resx("Resources/Errors.resx", "Error_Unknown")));
    }

    /// <summary>
    /// A dot in a file name is not a culture: <c>App.Strings.resx</c> is its own set, not a
    /// translation of <c>App.resx</c> into a language called Strings.
    /// </summary>
    [Fact]
    public void ADotThatIsNotACulture_DoesNotMakeATranslation()
    {
        Assert.Empty(AnalyzerTestHelper.Run(
            "PGL002",
            Empty,
            AnalyzerTestHelper.Resx("Resources/App.resx", "Home_Title"),
            AnalyzerTestHelper.Resx("Resources/App.Strings.resx", "Something_Else")));
    }

    /// <summary>The message has to be actionable, which means pointing at the key in the file.</summary>
    [Fact]
    public void TheDiagnostic_PointsAtTheKeyInTheNeutralFile()
    {
        IReadOnlyList<Diagnostic> reported = AnalyzerTestHelper.Run(
            "PGL002",
            Empty,
            AnalyzerTestHelper.Resx("Resources/Strings.resx", "Home_Subtitle"),
            AnalyzerTestHelper.Resx("Resources/Strings.es.resx"));

        Location location = Assert.Single(reported).Location;

        Assert.Equal("Resources/Strings.resx", location.GetLineSpan().Path);
        Assert.Equal("Home_Subtitle".Length, location.SourceSpan.Length);
    }
}

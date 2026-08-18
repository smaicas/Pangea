using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Localization.CodeAnalysis;

/// <summary>
/// What the analyzer says about a resource key that will not resolve to anything.
/// </summary>
/// <remarks>
/// A missing key is invisible at runtime by design: <c>GetString</c> returns the key itself so a
/// screen stays readable rather than going blank. That is the right behaviour and the reason
/// nothing ever reports it - the application shows <c>Home_Title</c> and carries on. These rules
/// are where that becomes visible, at the moment it can still be fixed.
/// <para>
/// Both are warnings. A key that resolves to nothing and a language that is missing one are
/// defects with no other symptom: nothing else in the build, and nothing at runtime, will ever
/// mention them. Turn one down to a suggestion in a project where translation lags the code.
/// </para>
/// </remarks>
internal static class LocalizationDiagnostics
{
    private const string Category = "CdCSharp.Pangea.Localization";

    internal static readonly DiagnosticDescriptor KeyNotFound = new(
        id: "PGL001",
        title: "The resource key is in no .resx file",
        messageFormat: "'{0}' is in none of the project's .resx files, so it will be shown to the user as it is written here",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <remarks>
    /// Reported once the whole compilation is known, because the question is about the resource
    /// files rather than about any one piece of code - which is what the <c>CompilationEnd</c> tag
    /// declares to the tooling.
    /// </remarks>
    internal static readonly DiagnosticDescriptor KeyNotTranslated = new(
        id: "PGL002",
        title: "The resource key is missing from a translation",
        messageFormat: "'{0}' is in '{1}' but not in {2}, so those cultures will fall back to the neutral text",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: null,
        helpLinkUri: null,
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}

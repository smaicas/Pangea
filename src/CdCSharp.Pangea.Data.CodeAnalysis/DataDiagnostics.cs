using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Data.CodeAnalysis;

/// <summary>
/// What the analyzer says about data access that compiles and then behaves badly.
/// </summary>
/// <remarks>
/// Each of these is invisible until it is not: a context registered with no engine fails on the
/// first query rather than at the call that forgot it, a context resolved from the container is a
/// leak that shows up as stale data weeks later, and a save inside a write runs twice with no
/// symptom at all. Warnings, because the repository builds clean and because a severity of
/// suggestion never reaches a build log.
/// </remarks>
internal static class DataDiagnostics
{
    private const string Category = "CdCSharp.Pangea.Data";

    internal static readonly DiagnosticDescriptor NoProvider = new(
        id: "PGD001",
        title: "The context is registered with no database engine",
        messageFormat:
            "'{0}' is registered without a provider, so building the container throws. Call db.UseSqlite() " +
            "inside AddPangeaDbContext, from the CdCSharp.Pangea.Data.Sqlite package.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <remarks>
    /// Reported once the whole compilation is known, because whether the resolution is a mistake
    /// depends on a registration somewhere else in it - which is what the <c>CompilationEnd</c> tag
    /// declares to the tooling.
    /// </remarks>
    internal static readonly DiagnosticDescriptor ContextResolvedFromContainer = new(
        id: "PGD002",
        title: "The DbContext is resolved from the container",
        messageFormat:
            "'{0}' is not registered as a service, deliberately: it would be a context living as long as the " +
            "process. Ask for IPangeaDbContext<{0}> instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: null,
        helpLinkUri: null,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    internal static readonly DiagnosticDescriptor RedundantSaveChanges = new(
        id: "PGD003",
        title: "SaveChanges inside WriteAsync",
        messageFormat:
            "WriteAsync saves the change itself once the callback returns, so this call runs the save twice",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

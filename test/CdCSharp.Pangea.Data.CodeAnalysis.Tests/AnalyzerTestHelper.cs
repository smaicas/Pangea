using CdCSharp.Pangea.Data.Sqlite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace CdCSharp.Pangea.Data.CodeAnalysis.Tests;

/// <summary>
/// Runs <see cref="DataUsageAnalyzer"/> over an in-memory compilation that references the real
/// feature, so a rename in the toolkit fails these tests rather than quietly disabling a rule.
/// </summary>
internal static class AnalyzerTestHelper
{
    /// <summary>The declarations every sample builds on, so each one is only the call under test.</summary>
    private const string Prelude = """
        using CdCSharp.Pangea.Data;
        using CdCSharp.Pangea.Data.Abstractions;
        using CdCSharp.Pangea.Data.Configuration;
        using CdCSharp.Pangea.Data.Sqlite;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Samples;

        public class Note
        {
            public int Id { get; set; }
        }

        public class AppDbContext : DbContext
        {
            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

            public DbSet<Note> Notes => Set<Note>();
        }
        """;

    public static IReadOnlyList<Diagnostic> Run(string id, string source)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "DataAnalyzerTests_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: [CSharpSyntaxTree.ParseText(Prelude + Environment.NewLine + source)],
            references: BuildReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The samples are hand-written; a typo in one would otherwise look like the analyzer
        // failing to report.
        Diagnostic[] errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, "The sample source did not compile:\n" +
            string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers([new DataUsageAnalyzer()]);

        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult()
            .Where(diagnostic => diagnostic.Id == id)
            .ToList();
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        string trusted = (string)(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty);

        List<MetadataReference> references = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        foreach (Type anchor in new[] { typeof(SqliteDbProvider), typeof(DbContext) })
        {
            string location = anchor.Assembly.Location;

            if (!string.IsNullOrEmpty(location) &&
                !references.Any(reference => (reference as PortableExecutableReference)?.FilePath == location))
            {
                references.Add(MetadataReference.CreateFromFile(location));
            }
        }

        return references;
    }
}

using CdCSharp.Pangea.Core.Base;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CdCSharp.Pangea.CodeGeneration.Tests;

/// <summary>
/// Runs <see cref="CatalogGenerator"/> over an in-memory compilation and hands back what it wrote.
/// </summary>
internal static class CatalogTestHelper
{
    /// <summary>The generated catalog, or null when the generator decided there was nothing to say.</summary>
    public static string? Run(string source, string assemblyName = "Sample.App")
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Diagnostic[] errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, "The sample source did not compile:\n" +
            string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        GeneratorDriverRunResult result = CSharpGeneratorDriver
            .Create(new CatalogGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        GeneratedSourceResult[] generated = result.Results
            .SelectMany(run => run.GeneratedSources)
            .ToArray();

        Assert.True(generated.Length <= 1, $"Expected at most one catalog, got {generated.Length}.");

        return generated.Length == 0 ? null : generated[0].SourceText.ToString();
    }

    /// <summary>The generated catalog, failing the test when nothing was generated.</summary>
    public static string RunExpectingCatalog(string source, string assemblyName = "Sample.App")
    {
        string? generated = Run(source, assemblyName);

        Assert.True(generated is not null, "The generator produced no catalog.");
        return generated!;
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        string trusted = (string)(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty);

        List<MetadataReference> references = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        foreach (Type anchor in new[] { typeof(ViewModelBase), typeof(Avalonia.Controls.Control) })
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

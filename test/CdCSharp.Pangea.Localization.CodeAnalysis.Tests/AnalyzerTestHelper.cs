using CdCSharp.Pangea.Localization.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace CdCSharp.Pangea.Localization.CodeAnalysis.Tests;

/// <summary>
/// Runs <see cref="ResourceKeyAnalyzer"/> over an in-memory compilation and a set of in-memory
/// <c>.resx</c> files, which is exactly the pair of inputs it is given during a build.
/// </summary>
internal static class AnalyzerTestHelper
{
    /// <summary>A .resx holding the given keys, named as the build would name it.</summary>
    public static (string Path, string Content) Resx(string path, params string[] keys)
    {
        string entries = string.Join(
            Environment.NewLine,
            keys.Select(key => $"""
                  <data name="{key}" xml:space="preserve">
                    <value>{key} text</value>
                  </data>
                """));

        return (path, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <resheader name="resmimetype">
                <value>text/microsoft-resx</value>
              </resheader>
            {entries}
            </root>
            """);
    }

    public static IReadOnlyList<Diagnostic> Run(string source, params (string Path, string Content)[] resources)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: BuildReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The sample sources are hand-written; a typo in one would otherwise look like the
        // analyzer failing to report.
        Diagnostic[] errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(errors.Length == 0, "The sample source did not compile:\n" +
            string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        AnalyzerOptions options = new(resources
            .Select(resource => (AdditionalText)new InMemoryText(resource.Path, resource.Content))
            .ToImmutableArray());

        CompilationWithAnalyzers withAnalyzers = compilation.WithAnalyzers(
            [new ResourceKeyAnalyzer()], options);

        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    /// <summary>The diagnostics of one rule, in the order they were reported.</summary>
    public static IReadOnlyList<Diagnostic> Run(string id, string source, params (string Path, string Content)[] resources) =>
        Run(source, resources).Where(diagnostic => diagnostic.Id == id).ToList();

    private sealed class InMemoryText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        string trusted = (string)(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty);

        List<MetadataReference> references = trusted
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        string localization = typeof(ILocalizationService).Assembly.Location;

        if (!references.Any(reference => (reference as PortableExecutableReference)?.FilePath == localization))
        {
            references.Add(MetadataReference.CreateFromFile(localization));
        }

        return references;
    }
}

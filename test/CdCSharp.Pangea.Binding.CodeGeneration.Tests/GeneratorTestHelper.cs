using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// Drives <see cref="ViewModelSourceGenerator"/> over an in-memory compilation and exposes
/// the generated sources so tests can assert on the emitted ViewModel code.
/// </summary>
internal static class GeneratorTestHelper
{
    public sealed record GeneratorResult(
        IReadOnlyList<GeneratedSource> Sources,
        IReadOnlyList<Diagnostic> Diagnostics,
        Compilation OutputCompilation);

    public sealed record GeneratedSource(string HintName, string Text);

    /// <summary>
    /// Mirrors the SDK's ImplicitUsings so sample ViewModels read like real project files.
    /// </summary>
    private static readonly SyntaxTree ImplicitUsings = CSharpSyntaxTree.ParseText("""
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Threading.Tasks;
        """);

    /// <summary>
    /// Runs the generator against <paramref name="source"/> and returns every generated file.
    /// </summary>
    public static GeneratorResult Run(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests_" + Guid.NewGuid().ToString("N"),
            syntaxTrees: new[] { ImplicitUsings, syntaxTree },
            references: BuildReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(new ViewModelSourceGenerator());

        GeneratorDriver ran = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out Compilation outputCompilation, out _);

        GeneratorDriverRunResult runResult = ran.GetRunResult();

        List<GeneratedSource> sources = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(gs => new GeneratedSource(gs.HintName, gs.SourceText.ToString()))
            .ToList();

        return new GeneratorResult(sources, runResult.Diagnostics.ToArray(), outputCompilation);
    }

    /// <summary>
    /// Convenience: returns the generated binding partial for the given class.
    /// Fails the calling test when the generator produced nothing for that class.
    /// </summary>
    public static string GetBindingSource(string source, string className)
    {
        string? generated = TryGetBindingSource(source, className);
        Assert.True(generated is not null, $"No binding source was generated for '{className}'.");
        return generated!;
    }

    /// <summary>
    /// Returns the generated binding partial for the given class, or null when none was emitted.
    /// </summary>
    /// <remarks>
    /// Matched on the tail of the file name: generated files are named after the fully qualified
    /// type, so that two view models of the same name in different namespaces do not collide, and
    /// callers here name the class alone.
    /// </remarks>
    public static string? TryGetBindingSource(string source, string className)
    {
        GeneratorResult result = Run(source);
        string suffix = $"{className}.Binding.g.cs";

        return result.Sources
            .FirstOrDefault(s => s.HintName == suffix || s.HintName.EndsWith("." + suffix, StringComparison.Ordinal))
            ?.Text;
    }

    /// <summary>
    /// Runs the generator and compiles the result, asserting the emitted code is valid C#.
    /// Returns the loaded assembly so tests can exercise the generated members at runtime.
    /// </summary>
    public static Assembly RunAndLoad(string source)
    {
        GeneratorResult result = Run(source);

        IReadOnlyList<Diagnostic> errors = GetErrors(result.OutputCompilation);
        Assert.True(errors.Count == 0, "Generated code did not compile:\n" + Describe(errors, result));

        using MemoryStream stream = new();
        EmitResult emit = result.OutputCompilation.Emit(stream);
        Assert.True(emit.Success, "Emit failed:\n" + Describe(emit.Diagnostics, result));

        return Assembly.Load(stream.ToArray());
    }

    /// <summary>
    /// Compilation errors produced after the generator ran. Empty means the generated code is valid.
    /// </summary>
    public static IReadOnlyList<Diagnostic> GetErrors(Compilation compilation) =>
        compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

    private static string Describe(IEnumerable<Diagnostic> diagnostics, GeneratorResult result)
    {
        string messages = string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));
        string generated = string.Join(
            Environment.NewLine,
            result.Sources.Select(s => $"----- {s.HintName} -----{Environment.NewLine}{s.Text}"));

        return messages + Environment.NewLine + generated;
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        // Start from every assembly the test runtime already trusts (BCL, etc.).
        string trusted = (string)(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty);

        List<MetadataReference> references = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        // Ensure the Pangea runtime types used by the sample ViewModels are resolvable.
        AddAssembly(references, typeof(BindingAttribute));
        AddAssembly(references, typeof(ViewModelBase));

        return references;
    }

    private static void AddAssembly(List<MetadataReference> references, Type type)
    {
        string location = type.Assembly.Location;
        if (!string.IsNullOrEmpty(location) &&
            !references.Any(r => (r as PortableExecutableReference)?.FilePath == location))
        {
            references.Add(MetadataReference.CreateFromFile(location));
        }
    }
}

using CdCSharp.Pangea;
using CdCSharp.Pangea.Binding.CodeGeneration;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;
using System.Text.RegularExpressions;

namespace CdCSharp.Pangea.Docs.Tests;

/// <summary>
/// Compiles every C# sample in the agent guide against the real assemblies.
/// </summary>
/// <remarks>
/// Documentation that teaches a coding agent is worse than useless when it drifts: the agent
/// produces confidently wrong code, at volume. Compiling the samples turns the guide from prose
/// into something the build can check.
/// </remarks>
public class AgentGuideCompilesTests
{
    /// <summary>Types the samples refer to without defining, standing in for application code.</summary>
    private const string Prelude = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Threading.Tasks;

        public interface IDataService;
        public class DataService : IDataService;
        public interface ITelemetry;
        public class Telemetry : ITelemetry;
        """;

    private static readonly Regex CSharpFence =
        new(@"^```csharp\r?$(?<code>.*?)^```\r?$", RegexOptions.Multiline | RegexOptions.Singleline);

    [Fact]
    public void EverySampleInTheGuideCompiles()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;

        IReadOnlyList<Sample> samples = ReadSamples();

        Assert.True(samples.Count >= 10, $"Only found {samples.Count} samples; the extraction is broken.");

        // One compilation for all of them: samples build on each other, exactly as a reader
        // accumulates context going down the page.
        CSharpCompilation compilation = CSharpCompilation.Create(
            "AgentGuideSamples",
            samples.Select(sample =>
                    CSharpSyntaxTree.ParseText(sample.Code, path: $"line {sample.Line}", cancellationToken: cancellation))
                .Prepend(CSharpSyntaxTree.ParseText(Prelude, cancellationToken: cancellation)),
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // The samples rely on [Binding] producing properties, so they are compiled the way a real
        // project builds them: through the generator.
        CSharpGeneratorDriver.Create(new ViewModelSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out Compilation generated, out _, cancellation);

        List<Diagnostic> errors = generated.GetDiagnostics(cancellation)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(errors.Count == 0, Describe(errors));
    }

    /// <summary>
    /// The template packs the skill straight from where it lives. Copying it into the template
    /// during the build looked equivalent and was not: MSBuild resolves item globs before any
    /// target runs, so the copy arrived after packing had already decided what to include.
    /// </summary>
    [Fact]
    public void TheTemplatePacksTheSkillFromItsSourceDirectory()
    {
        string project = File.ReadAllText(
            Path.Combine(DocsPaths.RepositoryRoot, "templates", "CdCSharp.Pangea.Templates.csproj"));

        Assert.Contains(@"..\.claude\skills\pangea\**\*.md", project);
        Assert.Contains(@"PackagePath=""content\MyPangeaApp\.claude\skills\pangea\", project);
        Assert.DoesNotContain("<Copy ", project);
    }

    private static IReadOnlyList<Sample> ReadSamples()
    {
        string markdown = File.ReadAllText(DocsPaths.AgentGuide);

        return CSharpFence.Matches(markdown)
            .Select(match => new Sample(
                Code: match.Groups["code"].Value,
                Line: markdown.Take(match.Index).Count(character => character == '\n') + 1))
            .ToList();
    }

    private static string Describe(IEnumerable<Diagnostic> errors)
    {
        StringBuilder message = new("The agent guide contains samples that do not compile:");
        message.AppendLine();

        foreach (Diagnostic error in errors)
        {
            FileLinePositionSpan span = error.Location.GetLineSpan();
            message.AppendLine($"  {span.Path} (+{span.StartLinePosition.Line}): {error.GetMessage()}");
        }

        return message.ToString();
    }

    private static IReadOnlyList<MetadataReference> References()
    {
        string trusted = (string)(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty);

        List<MetadataReference> references = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        // Everything the samples touch, in case the runtime has not loaded it yet.
        foreach (Type anchor in new[]
                 {
                     typeof(PangeaApplication), typeof(ViewModelBase), typeof(PangeaPalette),
                     typeof(IStorageService), typeof(Avalonia.Application), typeof(Avalonia.Media.Color)
                 })
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

    private sealed record Sample(string Code, int Line);
}

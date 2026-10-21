using CdCSharp.Pangea.Apps.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>The handler generator over in-memory compilations: what it writes, and what it refuses.</summary>
public class GeneratorTests
{
    private const string Usings = """
        using CdCSharp.Pangea.Apps;
        using System.Threading;
        using System.Threading.Tasks;
        """;

    [Fact]
    public void A_handler_is_registered_and_listed_in_the_manifest()
    {
        (string? source, IReadOnlyList<Diagnostic> diagnostics) = Run(Usings + """

            namespace Sample;

            [AppMessage("sample.greet")]
            public sealed record Greet(string Name) : IAppRequest<string>;

            internal sealed class GreetHandler : IAppRequestHandler<Greet, string>
            {
                public Task<string> HandleAsync(Greet request, AppRequestContext context, CancellationToken cancellationToken) =>
                    Task.FromResult("hola " + request.Name);
            }
            """);

        Assert.Empty(diagnostics);
        Assert.NotNull(source);
        Assert.Contains("[assembly: global::CdCSharp.Pangea.Apps.PangeaAppHandles(\"sample.greet\")]", source);
        Assert.Contains("AppHandlerEntry.Create<global::Sample.Greet, string, global::Sample.GreetHandler>(\"sample.greet\")", source);
        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", source);
    }

    [Fact]
    public void A_request_without_an_id_is_an_error()
    {
        (string? source, IReadOnlyList<Diagnostic> diagnostics) = Run(Usings + """

            public sealed record Anonymous : IAppRequest<bool>;

            internal sealed class AnonymousHandler : IAppRequestHandler<Anonymous, bool>
            {
                public Task<bool> HandleAsync(Anonymous request, AppRequestContext context, CancellationToken cancellationToken) => Task.FromResult(true);
            }
            """);

        Assert.Null(source);
        Assert.Equal("PGA001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void Two_handlers_for_one_message_is_an_error()
    {
        (_, IReadOnlyList<Diagnostic> diagnostics) = Run(Usings + """

            [AppMessage("sample.twice")]
            public sealed record Twice : IAppRequest<bool>;

            internal sealed class First : IAppRequestHandler<Twice, bool>
            {
                public Task<bool> HandleAsync(Twice request, AppRequestContext context, CancellationToken cancellationToken) => Task.FromResult(true);
            }

            internal sealed class Second : IAppRequestHandler<Twice, bool>
            {
                public Task<bool> HandleAsync(Twice request, AppRequestContext context, CancellationToken cancellationToken) => Task.FromResult(false);
            }
            """);

        Assert.Equal("PGA002", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void A_private_handler_is_an_error_rather_than_silently_skipped()
    {
        (_, IReadOnlyList<Diagnostic> diagnostics) = Run(Usings + """

            [AppMessage("sample.hidden")]
            public sealed record Hidden : IAppRequest<bool>;

            public static class Outer
            {
                private sealed class HiddenHandler : IAppRequestHandler<Hidden, bool>
                {
                    public Task<bool> HandleAsync(Hidden request, AppRequestContext context, CancellationToken cancellationToken) => Task.FromResult(true);
                }
            }
            """);

        Assert.Equal("PGA003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void A_protocol_library_without_the_runtime_generates_nothing()
    {
        (string? source, IReadOnlyList<Diagnostic> diagnostics) = Run(Usings + """

            [AppMessage("sample.only.declared")]
            public sealed record Declared : IAppRequest<bool>;
            """, includeRuntime: false);

        Assert.Null(source);
        Assert.Empty(diagnostics);
    }

    private static (string? Source, IReadOnlyList<Diagnostic> Diagnostics) Run(string source, bool includeRuntime = true)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Sample.App",
            [CSharpSyntaxTree.ParseText(source)],
            References(includeRuntime),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Diagnostic[] errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, "The sample did not compile:\n" + string.Join(Environment.NewLine, errors.Select(error => error.ToString())));

        GeneratorDriverRunResult result = CSharpGeneratorDriver.Create(new AppHandlerGenerator()).RunGenerators(compilation).GetRunResult();
        GeneratedSourceResult[] generated = result.Results.SelectMany(run => run.GeneratedSources).ToArray();

        return (generated.Length == 0 ? null : generated[0].SourceText.ToString(), result.Diagnostics);
    }

    private static IReadOnlyList<MetadataReference> References(bool includeRuntime)
    {
        string trusted = (string)(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty);

        List<string> paths = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => File.Exists(path) && !Path.GetFileName(path).StartsWith("CdCSharp.Pangea.Apps", StringComparison.Ordinal))
            .ToList();

        paths.Add(typeof(IAppBus).Assembly.Location);

        if (includeRuntime) paths.Add(typeof(AppHandlerCatalog).Assembly.Location);

        return paths.Distinct().Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
    }
}

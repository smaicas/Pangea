using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace CdCSharp.Pangea.Apps.CodeGeneration;

/// <summary>
/// Registers every request handler an application declares, and lists what it answers in its manifest.
/// </summary>
/// <remarks>
/// <para>
/// Two outputs from one pass. A module initializer adds an entry per handler to the runtime's
/// catalog, so answering a request is a generic call the compiler wrote rather than a type found
/// by reflection. And one <c>[assembly: PangeaAppHandles("id")]</c> per handler, so a host reading
/// the assembly's metadata knows which application to start for a request without starting it.
/// </para>
/// <para>
/// Nothing is generated in a project that does not reference the runtime: a protocol library
/// declares messages, not handlers.
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class AppHandlerGenerator : IIncrementalGenerator
{
    private const string HandlerInterface = "CdCSharp.Pangea.Apps.IAppRequestHandler`2";
    private const string MessageAttribute = "CdCSharp.Pangea.Apps.AppMessageAttribute";
    private const string CatalogType = "CdCSharp.Pangea.Apps.AppHandlerCatalog";

    private static readonly SymbolDisplayFormat FullyQualified =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol?> declarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null } or RecordDeclarationSyntax { BaseList: not null },
                static (syntax, cancellation) =>
                    syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation) as INamedTypeSymbol);

        IncrementalValueProvider<(Compilation Compilation, ImmutableArray<INamedTypeSymbol?> Types)> everything =
            context.CompilationProvider.Combine(declarations.Collect());

        context.RegisterSourceOutput(everything, static (production, input) => Generate(production, input.Compilation, input.Types));
    }

    private static void Generate(SourceProductionContext production, Compilation compilation, ImmutableArray<INamedTypeSymbol?> declared)
    {
        INamedTypeSymbol? handlerInterface = compilation.GetTypeByMetadataName(HandlerInterface);
        INamedTypeSymbol? messageAttribute = compilation.GetTypeByMetadataName(MessageAttribute);

        // Handlers only mean something where the runtime that runs them is referenced.
        if (handlerInterface is null || messageAttribute is null || compilation.GetTypeByMetadataName(CatalogType) is null) return;

        List<HandlerModel> handlers = new();
        Dictionary<string, HandlerModel> byMessage = new(System.StringComparer.Ordinal);
        HashSet<string> seen = new(System.StringComparer.Ordinal);

        foreach (INamedTypeSymbol? type in declared)
        {
            production.CancellationToken.ThrowIfCancellationRequested();

            if (type is null || type.IsAbstract || type.IsGenericType || type.TypeKind != TypeKind.Class) continue;

            string handlerName = type.ToDisplayString(FullyQualified);

            // A partial class is declared more than once.
            if (!seen.Add(handlerName)) continue;

            foreach (INamedTypeSymbol implemented in type.AllInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, handlerInterface)) continue;

                ITypeSymbol request = implemented.TypeArguments[0];
                ITypeSymbol response = implemented.TypeArguments[1];
                Location location = type.Locations.FirstOrDefault() ?? Location.None;

                string? messageId = MessageIdOf(request, messageAttribute);

                if (messageId is null)
                {
                    production.ReportDiagnostic(Diagnostic.Create(AppDiagnostics.MissingMessageId, location, request.Name, type.Name));
                    continue;
                }

                if (!IsReachable(type))
                {
                    production.ReportDiagnostic(Diagnostic.Create(AppDiagnostics.UnreachableHandler, location, type.Name));
                    continue;
                }

                HandlerModel handler = new(
                    messageId,
                    request.ToDisplayString(FullyQualified),
                    response.ToDisplayString(FullyQualified),
                    handlerName);

                if (byMessage.TryGetValue(messageId, out HandlerModel? existing))
                {
                    production.ReportDiagnostic(Diagnostic.Create(AppDiagnostics.DuplicateHandler, location, messageId, existing.HandlerType, handlerName));
                    continue;
                }

                byMessage[messageId] = handler;
                handlers.Add(handler);
            }
        }

        if (handlers.Count == 0) return;

        handlers.Sort((left, right) => System.StringComparer.Ordinal.Compare(left.MessageId, right.MessageId));
        production.AddSource("PangeaAppHandlers.g.cs", Write(handlers, Identifier(compilation.AssemblyName ?? "App")));
    }

    private static string? MessageIdOf(ITypeSymbol request, INamedTypeSymbol messageAttribute)
    {
        foreach (AttributeData attribute in request.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, messageAttribute) &&
                attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is string { Length: > 0 } id)
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Whether code generated into the same assembly can name the type: not private, nor inside something private.</summary>
    private static bool IsReachable(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal) return false;
        }

        return true;
    }

    private static string Write(List<HandlerModel> handlers, string assemblyIdentifier)
    {
        StringBuilder source = new();

        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();

        foreach (HandlerModel handler in handlers)
        {
            source.Append("[assembly: global::CdCSharp.Pangea.Apps.PangeaAppHandles(").Append(Literal(handler.MessageId)).AppendLine(")]");
        }

        source.AppendLine();
        source.Append("namespace CdCSharp.Pangea.Generated.Apps.").Append(assemblyIdentifier).AppendLine();
        source.AppendLine("{");
        source.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"CdCSharp.Pangea.Apps.CodeGeneration\", \"1.0\")]");
        source.AppendLine("    internal static class PangeaAppHandlers");
        source.AppendLine("    {");
        source.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        source.AppendLine("        internal static void Register()");
        source.AppendLine("        {");

        foreach (HandlerModel handler in handlers)
        {
            source.Append("            global::CdCSharp.Pangea.Apps.AppHandlerCatalog.Add(global::CdCSharp.Pangea.Apps.AppHandlerEntry.Create<")
                .Append(handler.RequestType).Append(", ")
                .Append(handler.ResponseType).Append(", ")
                .Append(handler.HandlerType).Append(">(")
                .Append(Literal(handler.MessageId)).AppendLine("));");
        }

        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");

        return source.ToString();
    }

    private static string Literal(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Identifier(string assemblyName)
    {
        StringBuilder builder = new(assemblyName.Length);

        foreach (char character in assemblyName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        if (builder.Length == 0 || char.IsDigit(builder[0])) builder.Insert(0, '_');

        return builder.ToString();
    }

    private sealed class HandlerModel
    {
        public HandlerModel(string messageId, string requestType, string responseType, string handlerType)
        {
            MessageId = messageId;
            RequestType = requestType;
            ResponseType = responseType;
            HandlerType = handlerType;
        }

        public string MessageId { get; }

        public string RequestType { get; }

        public string ResponseType { get; }

        public string HandlerType { get; }
    }
}

internal static class AppDiagnostics
{
    private const string Category = "CdCSharp.Pangea.Apps";

    public static readonly DiagnosticDescriptor MissingMessageId = new(
        "PGA001",
        "Request handled without a message id",
        "'{1}' handles '{0}', which has no [AppMessage(\"app.area.verb\")]; the other application has no id to send it by",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateHandler = new(
        "PGA002",
        "Two handlers for one message",
        "'{0}' is handled by both '{1}' and '{2}'; an application answers a message once",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnreachableHandler = new(
        "PGA003",
        "Handler cannot be registered",
        "'{0}' is private or nested inside a private type, so the generated registration cannot create it; make it internal or public",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

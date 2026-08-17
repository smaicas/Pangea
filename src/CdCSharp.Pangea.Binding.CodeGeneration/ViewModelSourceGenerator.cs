using Microsoft.CodeAnalysis;
using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace CdCSharp.Pangea.Binding.CodeGeneration;

/// <summary>
/// Generador de código profesional que utiliza análisis funcional completo
/// para generar propiedades con notificaciones automáticas optimizadas
/// </summary>
[Generator]
public class ViewModelSourceGenerator : IIncrementalGenerator
{
    private const string GENERATOR_VERSION = "v2.1.0";
    private const string BUILD_DATE = "2025-08-30";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ViewModelAnalysis?> viewModelAnalyses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (s, _) => IsCandidateViewModel(s),
                static (ctx, _) => AnalyzeViewModel(ctx))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(viewModelAnalyses, static (spc, analysis) => GenerateCode(analysis, spc));
    }

    private static bool IsCandidateViewModel(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax classDecl &&
               (HasBindingFields(classDecl) || HasCommandProperties(classDecl));
    }

    private static bool HasBindingFields(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members.OfType<FieldDeclarationSyntax>()
            .Any(f => f.AttributeLists.Any(al =>
                al.Attributes.Any(a => a.Name.ToString().Contains("Binding"))));
    }

    private static bool HasCommandProperties(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members.OfType<PropertyDeclarationSyntax>()
            .Any(p => p.Type.ToString().Contains("RelayCommand"));
    }

    /// <summary>
    /// Analyses the class this declaration belongs to, once, from all of its declarations.
    /// </summary>
    /// <remarks>
    /// The syntax provider fires per declaration, and a partial class has several. Generating from
    /// each one produced two files with the same name for one class - which makes Roslyn drop the
    /// generator for the entire compilation - and each analysis only ever saw its own half of the
    /// class. One declaration is elected to do the work for all of them.
    /// </remarks>
    private static ViewModelAnalysis? AnalyzeViewModel(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration) return null;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        List<ClassDeclarationSyntax> declarations = classSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .OrderBy(declaration => declaration.SyntaxTree.FilePath, StringComparer.Ordinal)
            .ThenBy(declaration => declaration.SpanStart)
            .ToList();

        if (declarations.Count == 0) declarations.Add(classDeclaration);

        // Only the first declaration that is itself a candidate generates: the others would repeat
        // the same output. Elected among candidates, because a class can keep its [Binding] fields
        // in a later file than the one it was opened in.
        ClassDeclarationSyntax? elected = declarations.FirstOrDefault(IsCandidateViewModel);

        if (elected is null || elected != classDeclaration) return null;

        Compilation compilation = context.SemanticModel.Compilation;

        List<ViewModelPart> parts = declarations
            .Select(declaration => new ViewModelPart(
                declaration,
                declaration.SyntaxTree == context.SemanticModel.SyntaxTree
                    ? context.SemanticModel
                    : compilation.GetSemanticModel(declaration.SyntaxTree)))
            .ToList();

        FunctionalAnalyzer analyzer = new FunctionalAnalyzer();
        return analyzer.AnalyzeViewModel(parts);
    }

    private static void GenerateCode(ViewModelAnalysis? analysis, SourceProductionContext context)
    {
        if (analysis == null) return;

        // A class can have nothing of its own to generate and still need the forwarding override:
        // a screen deriving from a shared view model, computing from what the base declares.
        bool hasSomethingToEmit = analysis.BindingFields.Count > 0 ||
                                  analysis.InheritedDependencyNotifications.Count > 0 ||
                                  analysis.InheritedCommandNotifications.Count > 0;

        if (!hasSomethingToEmit) return;

        foreach (Diagnostic diagnostic in analysis.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }

        // Generating on top of an error buries it: the author would get one message they can act on
        // and several more about a file they did not write.
        if (analysis.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return;
        }

        string source = GenerateClassSource(analysis);
        // Qualified, not just the class name: two view models called DetailViewModel in different
        // feature namespaces is ordinary, and a repeated hint name makes Roslyn abandon the whole
        // generator - every [Binding] in the project silently stops being generated.
        string hint = SanitiseHintName(analysis.FullyQualifiedName);

        context.AddSource($"{hint}.Binding.g.cs", SourceText.From(source, Encoding.UTF8));

        // Generar archivo de debug con información del análisis (solo en DEBUG)
#if DEBUG
        string debugInfo = GenerateDebugInfo(analysis);
        context.AddSource($"{hint}.Analysis.Debug.g.cs", SourceText.From(debugInfo, Encoding.UTF8));
#endif
    }

    private static string GenerateClassSource(ViewModelAnalysis analysis)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine(
            $"// Generated by Pangea Binding Feature with Professional Functional Analysis {GENERATOR_VERSION}");
        sb.AppendLine($"// Build Date: {BUILD_DATE}");
        sb.AppendLine("// This file contains optimized property implementations with automatic notifications");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine();
        // A view model in the global namespace has none to declare, and emitting an empty one
        // produces a file that does not parse.
        if (!string.IsNullOrEmpty(analysis.Namespace))
        {
            sb.AppendLine($"namespace {analysis.Namespace};");
            sb.AppendLine();
        }

        // Enclosing types are re-opened around it: without them the partial describes a different,
        // top-level type and nothing it generates can see the fields it is meant to wrap.
        foreach (string container in analysis.ContainingTypes)
        {
            sb.AppendLine($"partial class {container}");
            sb.AppendLine("{");
        }

        sb.AppendLine($"partial class {analysis.ClassName}{analysis.TypeParameters}");
        sb.AppendLine("{");

        // Generar propiedades con análisis funcional completo
        foreach (BindingFieldInfo field in analysis.BindingFields)
        {
            GenerateOptimizedProperty(sb, field, analysis);
        }

        // Generar declaraciones de métodos parciales
        foreach (BindingFieldInfo field in analysis.BindingFields.Where(f => !f.ReadOnly))
        {
            sb.AppendLine($"    partial void On{field.PropertyName}Changed();");
        }

        GenerateInheritedDependencyForwarding(sb, analysis);

        sb.AppendLine("}");

        // One closing brace per enclosing type opened above.
        foreach (string _ in analysis.ContainingTypes)
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Forwards notifications for properties this class depends on but did not declare.
    /// </summary>
    /// <remarks>
    /// The base class raises its own property; it cannot know that a subclass computed something
    /// from it. Overriding OnPropertyChanged is where the subclass gets to find out.
    /// </remarks>
    private static void GenerateInheritedDependencyForwarding(StringBuilder sb, ViewModelAnalysis analysis)
    {
        if (analysis.InheritedDependencyNotifications.Count == 0 &&
            analysis.InheritedCommandNotifications.Count == 0)
        {
            return;
        }

        IEnumerable<string> inheritedProperties = analysis.InheritedDependencyNotifications.Keys
            .Concat(analysis.InheritedCommandNotifications.Keys)
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal);

        sb.AppendLine();
        sb.AppendLine("    /// <summary>Raises what this class derives from properties declared by a base class.</summary>");
        sb.AppendLine("    protected override void OnPropertyChanged(string? propertyName = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnPropertyChanged(propertyName);");
        sb.AppendLine();
        sb.AppendLine("        switch (propertyName)");
        sb.AppendLine("        {");

        foreach (string inherited in inheritedProperties)
        {
            sb.AppendLine($"            case nameof({inherited}):");

            if (analysis.InheritedDependencyNotifications.TryGetValue(inherited, out List<string>? dependents))
            {
                foreach (string dependent in dependents.OrderBy(name => name, StringComparer.Ordinal))
                {
                    // base, not this: the dependent is not itself a base property, so re-entering
                    // the switch would only look for it and find nothing.
                    sb.AppendLine($"                base.OnPropertyChanged(nameof({dependent}));");
                }
            }

            if (analysis.InheritedCommandNotifications.TryGetValue(inherited, out List<string>? commands))
            {
                foreach (string command in commands.OrderBy(name => name, StringComparer.Ordinal))
                {
                    sb.AppendLine($"                {command}.RaiseCanExecuteChanged();");
                }
            }

            sb.AppendLine("                break;");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    /// <summary>Turns a qualified type name into something usable as a file name.</summary>
    private static string SanitiseHintName(string fullyQualifiedName)
    {
        StringBuilder safe = new StringBuilder(fullyQualifiedName.Length);

        foreach (char character in fullyQualifiedName)
        {
            safe.Append(char.IsLetterOrDigit(character) || character == '.' || character == '_'
                ? character
                : '_');
        }

        return safe.ToString();
    }

    private static void GenerateOptimizedProperty(StringBuilder sb, BindingFieldInfo field, ViewModelAnalysis analysis)
    {
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Gets or sets {field.PropertyName}");
        if (analysis.NotificationRequirements.TryGetValue(field.PropertyName,
                out NotificationRequirements? requirements))
        {
            int totalNotifications = requirements.ComputedPropertyNotifications.Count +
                                     requirements.CommandNotifications.Count +
                                     requirements.CollectionDependentNotifications.Count;
            if (totalNotifications > 0)
            {
                sb.AppendLine(
                    $"    /// Automatically triggers {totalNotifications} dependent notifications when changed");
            }
        }

        sb.AppendLine($"    /// </summary>");

        sb.AppendLine($"    public {field.FieldType} {field.PropertyName}");
        sb.AppendLine("    {");
        sb.AppendLine($"        get => {field.FieldName};");

        if (!field.ReadOnly)
        {
            sb.AppendLine("        set");
            sb.AppendLine("        {");
            sb.AppendLine($"            if (SetProperty(ref {field.FieldName}, value))");
            sb.AppendLine("            {");

            // Llamar método parcial
            sb.AppendLine($"                On{field.PropertyName}Changed();");

            // Generar notificaciones basadas en análisis funcional
            if (requirements != null)
            {
                GenerateComputedPropertyNotifications(sb, requirements.ComputedPropertyNotifications);
                GenerateCommandNotifications(sb, requirements.CommandNotifications);
                GenerateCollectionDependentNotifications(sb, requirements.CollectionDependentNotifications);
            }

            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void GenerateComputedPropertyNotifications(StringBuilder sb, List<string> computedProperties)
    {
        if (!computedProperties.Any()) return;

        sb.AppendLine();
        sb.AppendLine("                // Computed property notifications");

        foreach (string computedProp in computedProperties.OrderBy(cp => cp))
        {
            sb.AppendLine($"                OnPropertyChanged(nameof({computedProp}));");
        }
    }

    private static void GenerateCommandNotifications(StringBuilder sb, List<string> commandNotifications)
    {
        if (commandNotifications.Any())
        {
            sb.AppendLine();
            sb.AppendLine("                // Command CanExecute notifications");
            foreach (string commandName in commandNotifications.OrderBy(c => c))
            {
                sb.AppendLine($"                {commandName}.RaiseCanExecuteChanged();");
            }
        }
    }

    private static void GenerateCollectionDependentNotifications(StringBuilder sb, List<string> collectionNotifications)
    {
        if (!collectionNotifications.Any()) return;

        sb.AppendLine();
        sb.AppendLine("                // Collection-dependent notifications");

        foreach (string notification in collectionNotifications.OrderBy(n => n))
        {
            sb.AppendLine($"                OnPropertyChanged(nameof({notification}));");
        }
    }

#if DEBUG
    private static string GenerateDebugInfo(ViewModelAnalysis analysis)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("/*");
        sb.AppendLine($" * DEBUG: Functional Analysis Report {GENERATOR_VERSION}");
        sb.AppendLine($" * Class: {analysis.ClassName}");
        sb.AppendLine($" * Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($" * Build Date: {BUILD_DATE}");
        sb.AppendLine(" * ===================================");
        sb.AppendLine();

        sb.AppendLine(" * BINDING FIELDS:");
        foreach (BindingFieldInfo binding in analysis.BindingFields)
        {
            sb.AppendLine($" *   {binding.PropertyName} ({binding.FieldType}) - ReadOnly: {binding.ReadOnly}");
        }

        sb.AppendLine();

        sb.AppendLine(" * COMPUTED PROPERTIES:");
        foreach (ComputedPropertyInfo computed in analysis.ComputedProperties)
        {
            sb.AppendLine($" *   {computed.PropertyName}:");
            sb.AppendLine($" *     Dependencies: [{string.Join(", ", computed.DirectDependencies)}]");
        }

        sb.AppendLine();

        sb.AppendLine(" * CAN EXECUTE METHODS/PROPERTIES:");
        foreach (CanExecuteMethodInfo canExecute in analysis.CanExecuteMethods)
        {
            sb.AppendLine($" *   {canExecute.MethodName}:");
            sb.AppendLine($" *     Dependencies: [{string.Join(", ", canExecute.DirectDependencies)}]");
        }

        sb.AppendLine();

        sb.AppendLine(" * COMMANDS:");
        foreach (CommandInfo command in analysis.Commands)
        {
            sb.AppendLine($" *   {command.PropertyName}:");
            sb.AppendLine($" *     CanExecute References: [{string.Join(", ", command.CanExecuteReferences)}]");
            sb.AppendLine($" *     Binding Dependencies: [{string.Join(", ", command.DirectDependencies)}]");
        }

        sb.AppendLine();

        sb.AppendLine(" * TRANSITIVE DEPENDENCIES:");
        foreach (KeyValuePair<string, List<string>> kvp in analysis.TransitiveDependencies)
        {
            if (kvp.Value.Any())
            {
                sb.AppendLine($" *   {kvp.Key} depends on: [{string.Join(", ", kvp.Value)}]");
            }
        }

        sb.AppendLine();

        sb.AppendLine(" * NOTIFICATION REQUIREMENTS:");
        foreach (KeyValuePair<string, NotificationRequirements> kvp in analysis.NotificationRequirements)
        {
            NotificationRequirements req = kvp.Value;
            int totalNotifications = req.ComputedPropertyNotifications.Count +
                                     req.CommandNotifications.Count +
                                     req.CollectionDependentNotifications.Count;

            sb.AppendLine($" *   {kvp.Key} -> {totalNotifications} notifications:");

            if (req.ComputedPropertyNotifications.Any())
            {
                sb.AppendLine($" *     Computed: [{string.Join(", ", req.ComputedPropertyNotifications)}]");
            }

            if (req.CommandNotifications.Any())
            {
                sb.AppendLine($" *     Commands: [{string.Join(", ", req.CommandNotifications)}]");
            }

            if (req.CollectionDependentNotifications.Any())
            {
                sb.AppendLine($" *     Collections: [{string.Join(", ", req.CollectionDependentNotifications)}]");
            }
        }

        sb.AppendLine(" */");
        sb.AppendLine();
        sb.AppendLine("// This debug information is only generated in DEBUG builds");
        sb.AppendLine("// and helps verify that the functional analysis is working correctly.");

        return sb.ToString();
    }
#endif
}
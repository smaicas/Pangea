using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CdCSharp.Pangea.Localization.CodeAnalysis;

/// <summary>
/// Checks resource keys against the project's <c>.resx</c> files.
/// </summary>
/// <remarks>
/// Two questions, asked where they can still be answered cheaply: is this key defined at all, and
/// is it defined in every language the project ships. Both are invisible at runtime - a missing
/// key shows as the key, and a missing translation shows as the neutral text - which is why
/// nothing else reports them.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResourceKeyAnalyzer : DiagnosticAnalyzer
{
    private const string KeyAttribute = "CdCSharp.Pangea.Localization.Abstractions.LocalizationKeyAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            LocalizationDiagnostics.KeyNotFound,
            LocalizationDiagnostics.KeyNotTranslated);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        ResourceIndex index = ResourceIndex.Build(context.Options.AdditionalFiles, context.CancellationToken);

        // Without the .resx files there is nothing to check against. This is the state of a project
        // that has not been told to pass them to the compiler, and reporting every key in it as
        // missing would be worse than saying nothing.
        if (index.IsEmpty) return;

        INamedTypeSymbol? keyAttribute = context.Compilation.GetTypeByMetadataName(KeyAttribute);

        if (keyAttribute is not null)
        {
            context.RegisterOperationAction(
                operation => CheckKeys(operation, index, keyAttribute),
                OperationKind.Invocation,
                OperationKind.PropertyReference,
                OperationKind.ObjectCreation);
        }

        context.RegisterCompilationEndAction(compilation => ReportMissingTranslations(compilation, index));
    }

    /// <summary>
    /// Reports every constant argument bound to a <c>[LocalizationKey]</c> parameter that names
    /// nothing.
    /// </summary>
    private static void CheckKeys(OperationAnalysisContext context, ResourceIndex index, INamedTypeSymbol keyAttribute)
    {
        ImmutableArray<IArgumentOperation> arguments = context.Operation switch
        {
            IInvocationOperation invocation => invocation.Arguments,
            IPropertyReferenceOperation property => property.Arguments,
            IObjectCreationOperation creation => creation.Arguments,
            _ => ImmutableArray<IArgumentOperation>.Empty
        };

        foreach (IArgumentOperation argument in arguments)
        {
            if (argument.Parameter is not { } parameter || !IsKeyParameter(parameter, keyAttribute)) continue;

            // Only a constant can be checked. A key built at runtime is a deliberate choice to
            // decide it later, and guessing at it would report on code that is doing nothing wrong.
            if (argument.Value.ConstantValue is not { HasValue: true, Value: string key }) continue;

            if (key.Length == 0 || index.AllKeys.Contains(key)) continue;

            context.ReportDiagnostic(Diagnostic.Create(
                LocalizationDiagnostics.KeyNotFound, argument.Value.Syntax.GetLocation(), key));
        }
    }

    /// <summary>
    /// The attribute may be declared on the parameter, or inherited from the interface member the
    /// method implements - which is where <c>ILocalizationService.GetString</c> carries it.
    /// </summary>
    private static bool IsKeyParameter(IParameterSymbol parameter, INamedTypeSymbol keyAttribute)
    {
        if (HasAttribute(parameter, keyAttribute)) return true;

        if (parameter.ContainingSymbol is not IMethodSymbol method) return false;

        int position = parameter.Ordinal;

        foreach (IMethodSymbol declaration in InterfaceDeclarationsOf(method))
        {
            if (position < declaration.Parameters.Length &&
                HasAttribute(declaration.Parameters[position], keyAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(IParameterSymbol parameter, INamedTypeSymbol attribute) =>
        parameter.GetAttributes().Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attribute));

    /// <summary>The interface members <paramref name="method"/> implements, if any.</summary>
    private static IEnumerable<IMethodSymbol> InterfaceDeclarationsOf(IMethodSymbol method)
    {
        INamedTypeSymbol? containingType = method.ContainingType;

        if (containingType is null) yield break;

        foreach (INamedTypeSymbol contract in containingType.AllInterfaces)
        {
            foreach (ISymbol member in contract.GetMembers(method.Name))
            {
                if (member is IMethodSymbol candidate &&
                    SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidate), method))
                {
                    yield return candidate;
                }
            }
        }
    }

    /// <summary>
    /// Reports keys the neutral file defines and a translation does not, on the neutral file's own
    /// line for the key.
    /// </summary>
    private static void ReportMissingTranslations(CompilationAnalysisContext context, ResourceIndex index)
    {
        foreach (ResourceGroup group in index.Groups)
        {
            if (group.Neutral is not { } neutral || group.Translations.Count == 0) continue;

            foreach (KeyValuePair<string, Location> entry in neutral.Keys)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                string[] missing = group.Translations
                    .Where(translation => !translation.Keys.ContainsKey(entry.Key))
                    .Select(translation => translation.FileName)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();

                if (missing.Length == 0) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    LocalizationDiagnostics.KeyNotTranslated,
                    entry.Value,
                    entry.Key,
                    neutral.FileName,
                    string.Join(", ", missing)));
            }
        }
    }
}

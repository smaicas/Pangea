using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace CdCSharp.Pangea.Data.CodeAnalysis;

/// <summary>
/// Checks the three ways an application misuses the data feature and finds out later than it
/// should: a context with no engine, a context resolved from the container, and a save inside a
/// write that already saves.
/// </summary>
/// <remarks>
/// Nothing here runs in a project that does not reference <c>CdCSharp.Pangea.Data</c>: the whole
/// analysis is behind a lookup of the registration method, so an application with no database pays
/// one symbol lookup per compilation.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataUsageAnalyzer : DiagnosticAnalyzer
{
    private const string RegistrationExtensions = "CdCSharp.Pangea.Data.PangeaDataServiceCollectionExtensions";
    private const string BuilderType = "CdCSharp.Pangea.Data.Configuration.PangeaDbBuilder";
    private const string PangeaDbContextInterface = "CdCSharp.Pangea.Data.Abstractions.IPangeaDbContext`1";
    private const string DbContextType = "Microsoft.EntityFrameworkCore.DbContext";

    private const string RegistrationMethod = "AddPangeaDbContext";
    private const string WriteMethod = "WriteAsync";

    /// <summary>Entity Framework's own ways of registering a context with the container.</summary>
    private static readonly ImmutableHashSet<string> EntityFrameworkRegistrations = ImmutableHashSet.Create(
        "AddDbContext", "AddDbContextPool", "AddDbContextFactory", "AddPooledDbContextFactory");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DataDiagnostics.NoProvider,
            DataDiagnostics.ContextResolvedFromContainer,
            DataDiagnostics.RedundantSaveChanges);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>
    /// The context types the application registered with Entity Framework itself, and the places it
    /// resolved a context from the container. Which of the second are mistakes is only known once
    /// the whole compilation has been read.
    /// </summary>
    private sealed class Registrations
    {
        public ConcurrentDictionary<INamedTypeSymbol, byte> RegisteredWithEntityFramework { get; } =
            new(SymbolEqualityComparer.Default);

        public ConcurrentBag<(INamedTypeSymbol Context, Location Location)> Resolutions { get; } = new();
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        INamedTypeSymbol? registration = context.Compilation.GetTypeByMetadataName(RegistrationExtensions);

        // The project does not use the feature, so none of these questions apply to it.
        if (registration is null) return;

        INamedTypeSymbol? builder = context.Compilation.GetTypeByMetadataName(BuilderType);
        INamedTypeSymbol? pangeaDbContext = context.Compilation.GetTypeByMetadataName(PangeaDbContextInterface);
        INamedTypeSymbol? dbContext = context.Compilation.GetTypeByMetadataName(DbContextType);

        if (builder is null || pangeaDbContext is null || dbContext is null) return;

        Registrations registrations = new();

        context.RegisterOperationAction(
            operation => Check(
                (IInvocationOperation)operation.Operation, operation, registrations,
                registration, builder, pangeaDbContext, dbContext),
            OperationKind.Invocation);

        context.RegisterCompilationEndAction(compilation => ReportResolutions(compilation, registrations));
    }

    private static void Check(
        IInvocationOperation invocation,
        OperationAnalysisContext context,
        Registrations registrations,
        INamedTypeSymbol registration,
        INamedTypeSymbol builder,
        INamedTypeSymbol pangeaDbContext,
        INamedTypeSymbol dbContext)
    {
        IMethodSymbol method = invocation.TargetMethod;

        if (method.Name == RegistrationMethod &&
            SymbolEqualityComparer.Default.Equals(method.ContainingType, registration))
        {
            CheckProviderIsChosen(invocation, context, builder);
            return;
        }

        // An application may use this feature for one database and Entity Framework's own
        // registration for another. A context it registered itself is a context it is entitled to
        // resolve, so noting those is what keeps the rule below from reporting them.
        if (EntityFrameworkRegistrations.Contains(method.Name) && method.TypeArguments.Length >= 1)
        {
            if (method.TypeArguments[0] is INamedTypeSymbol registered && DerivesFrom(registered, dbContext))
            {
                registrations.RegisteredWithEntityFramework.TryAdd(registered, 0);
            }

            return;
        }

        if (method.Name is "GetRequiredService" or "GetService")
        {
            CollectResolvedType(invocation, registrations, dbContext);
            return;
        }

        if (method.Name is "SaveChanges" or "SaveChangesAsync" && DerivesFrom(method.ContainingType, dbContext))
        {
            CheckSaveInsideWrite(invocation, context, pangeaDbContext);
        }
    }

    private static void ReportResolutions(CompilationAnalysisContext context, Registrations registrations)
    {
        foreach ((INamedTypeSymbol resolved, Location location) in registrations.Resolutions)
        {
            if (registrations.RegisteredWithEntityFramework.ContainsKey(resolved)) continue;

            context.ReportDiagnostic(Diagnostic.Create(
                DataDiagnostics.ContextResolvedFromContainer, location, resolved.Name));
        }
    }

    /// <summary>
    /// Reports an <c>AddPangeaDbContext</c> whose callback never names an engine.
    /// </summary>
    /// <remarks>
    /// Only when the callback is written on the spot. A method group or a variable is configuration
    /// this cannot see through, and guessing there would report the applications that factored the
    /// registration out.
    /// </remarks>
    private static void CheckProviderIsChosen(
        IInvocationOperation invocation, OperationAnalysisContext context, INamedTypeSymbol builder)
    {
        IAnonymousFunctionOperation? callback = invocation.Arguments
            .Select(argument => Unwrap(argument.Value))
            .OfType<IAnonymousFunctionOperation>()
            .FirstOrDefault();

        if (callback is null) return;

        // A provider is chosen by a Use... method a provider package puts on the builder, and every
        // one of them returns the builder. That is what makes this a question about the return type
        // rather than a list of method names the feature would have to know in advance.
        bool chosen = callback.Descendants()
            .OfType<IInvocationOperation>()
            .Any(call => SymbolEqualityComparer.Default.Equals(call.TargetMethod.ReturnType, builder));

        if (chosen) return;

        string contextName = invocation.TargetMethod.TypeArguments.Length == 1
            ? invocation.TargetMethod.TypeArguments[0].Name
            : "The context";

        context.ReportDiagnostic(Diagnostic.Create(
            DataDiagnostics.NoProvider, invocation.Syntax.GetLocation(), contextName));
    }

    private static void CollectResolvedType(
        IInvocationOperation invocation, Registrations registrations, INamedTypeSymbol dbContext)
    {
        if (invocation.TargetMethod.TypeArguments.Length != 1) return;

        if (invocation.TargetMethod.TypeArguments[0] is not INamedTypeSymbol resolved) return;

        if (!DerivesFrom(resolved, dbContext)) return;

        registrations.Resolutions.Add((resolved, invocation.Syntax.GetLocation()));
    }

    /// <summary>
    /// Reports a save written inside the callback of <c>IPangeaDbContext.WriteAsync</c>, which
    /// saves once the callback returns.
    /// </summary>
    private static void CheckSaveInsideWrite(
        IInvocationOperation invocation, OperationAnalysisContext context, INamedTypeSymbol pangeaDbContext)
    {
        for (IOperation? parent = invocation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is not IAnonymousFunctionOperation) continue;

            // The lambda this save is written in. Whose argument it is decides whether the save is
            // redundant: the same call inside a lambda passed to anything else is ordinary code.
            IOperation? owner = parent.Parent;

            while (owner is IDelegateCreationOperation or IConversionOperation) owner = owner.Parent;

            if (owner is not IArgumentOperation argument) return;

            if (argument.Parent is not IInvocationOperation outer) return;

            if (outer.TargetMethod.Name == WriteMethod &&
                SymbolEqualityComparer.Default.Equals(outer.TargetMethod.ContainingType.OriginalDefinition, pangeaDbContext))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DataDiagnostics.RedundantSaveChanges, invocation.Syntax.GetLocation()));
            }

            return;
        }
    }

    /// <summary>The lambda inside the conversion the compiler wrapped it in.</summary>
    private static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IDelegateCreationOperation delegateCreation:
                    operation = delegateCreation.Target;
                    continue;

                case IConversionOperation conversion:
                    operation = conversion.Operand;
                    continue;

                default:
                    return operation;
            }
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
        }

        return false;
    }
}

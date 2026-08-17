using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Binding.CodeGeneration;

/// <summary>
/// What the generator says when it cannot do what <c>[Binding]</c> asked for.
/// </summary>
/// <remarks>
/// Without these the compiler speaks instead, and it talks about the generated file: a view model
/// that forgets to derive from <c>ViewModelBase</c> is reported as "the name 'SetProperty' does not
/// exist", in code the author never wrote and cannot edit. Each descriptor here names the field and
/// the fix, and the generator stops emitting for that class so one clear error replaces a cascade
/// of obscure ones.
/// </remarks>
internal static class BindingDiagnostics
{
    private const string Category = "CdCSharp.Pangea.Binding";

    internal static readonly DiagnosticDescriptor ClassMustBePartial = new(
        id: "PGB001",
        title: "A view model with [Binding] fields must be partial",
        messageFormat: "'{0}' has [Binding] fields, so it must be declared 'partial' for the generated properties to join it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ClassMustDeriveFromViewModelBase = new(
        id: "PGB002",
        title: "A view model with [Binding] fields needs a base that raises change notifications",
        messageFormat: "'{0}' has [Binding] fields but inherits no SetProperty and OnPropertyChanged for the generated properties to call; derive from ViewModelBase, or from a base that provides them",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DuplicatePropertyName = new(
        id: "PGB003",
        title: "Two [Binding] fields produce the same property",
        messageFormat: "'{0}' would generate property '{1}', which another [Binding] field in this class already generates",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PropertyNameAlreadyTaken = new(
        id: "PGB004",
        title: "The generated property name is already declared",
        messageFormat: "'{0}' would generate property '{1}', but this class already declares a member with that name",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PropertyHidesInheritedMember = new(
        id: "PGB006",
        title: "The generated property hides a member of a base class",
        messageFormat: "'{0}' generates property '{1}', which hides the '{1}' declared by '{2}'; rename the field, or the base member becomes unreachable through this class",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor StaticFieldNotSupported = new(
        id: "PGB005",
        title: "[Binding] does not apply to static fields",
        messageFormat: "'{0}' is static; [Binding] generates instance properties that raise change notifications, so the attribute is ignored here",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

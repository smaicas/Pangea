using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CdCSharp.Pangea.Binding.CodeGeneration;

/// <summary>
/// Analizador funcional profesional que detecta correctamente todas las dependencias 
/// entre propiedades, computed properties, métodos CanExecute y comandos mediante 
/// análisis técnico profundo de la estructura de la clase
/// </summary>
public class FunctionalAnalyzer
{
    /// <summary>
    /// Every declaration of the class under analysis. A partial class is one type however many
    /// files it is written across, and a member lookup that only sees one of them misses the rest.
    /// </summary>
    private IReadOnlyList<ClassDeclarationSyntax> _declarations = new List<ClassDeclarationSyntax>();

    public ViewModelAnalysis AnalyzeViewModel(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel) =>
        AnalyzeViewModel(new[] { new ViewModelPart(classDeclaration, semanticModel) });

    /// <summary>
    /// Analyses a class from all of its declarations at once.
    /// </summary>
    /// <remarks>
    /// The generator used to run once per declaration, which meant a class split across files was
    /// analysed twice, each time seeing half of itself - and both halves asked to write the same
    /// output file, which makes Roslyn drop the generator for the whole compilation.
    /// </remarks>
    public ViewModelAnalysis AnalyzeViewModel(IReadOnlyList<ViewModelPart> parts)
    {
        _declarations = parts.Select(part => part.Declaration).ToList();

        ClassDeclarationSyntax primary = parts[0].Declaration;

        ViewModelAnalysis analysis = new ViewModelAnalysis
        {
            ClassName = primary.Identifier.ValueText,
            Namespace = GetNamespace(primary),
            TypeParameters = primary.TypeParameterList?.ToString() ?? "",
            ContainingTypes = GetContainingTypes(primary)
        };

        // Phase 1: Inventory - Detectar todos los elementos funcionales
        foreach (ViewModelPart part in parts)
        {
            InventoryBindingFields(part.Declaration, part.SemanticModel, analysis);
        }

        // Validation needs the inventory: two of the checks are about the names it produced.
        Validate(parts, analysis);

        foreach (ViewModelPart part in parts)
        {
            InventoryComputedProperties(part.Declaration, analysis);
            InventoryCanExecuteElements(part.Declaration, analysis);
            InventoryCommands(part.Declaration, analysis);
            InventoryPartialVoidMethods(part.Declaration, analysis);
            InventoryCollectionModifyingMethods(part.Declaration, analysis);
        }

        // Phase 2: Dependency Analysis - Construir grafo completo de dependencias
        BuildCompleteDependencyGraph(analysis);

        // Phase 3: Command Analysis - Analizar dependencias de comandos hacia binding fields
        AnalyzeCommandDependencies(analysis);

        // Phase 4: Generate notification requirements
        CalculateNotificationRequirements(analysis);

        // Phase 5: dependencies on properties this class did not declare
        CalculateInheritedDependencies(parts, analysis);

        return analysis;
    }

    #region Phase 1: Inventory

    private void InventoryBindingFields(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel,
        ViewModelAnalysis analysis)
    {
        IEnumerable<FieldDeclarationSyntax> bindingFields = classDeclaration.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => HasBindingAttribute(f));

        foreach (FieldDeclarationSyntax field in bindingFields)
        {
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                if (semanticModel.GetDeclaredSymbol(variable) is IFieldSymbol fieldSymbol)
                {
                    BindingFieldInfo bindingInfo = ExtractBindingInfo(fieldSymbol);
                    analysis.BindingFields.Add(bindingInfo);
                }
            }
        }
    }

    private void InventoryComputedProperties(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
    {
        IEnumerable<PropertyDeclarationSyntax> computedProperties = classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => (p.ExpressionBody != null || HasComputedPropertyBody(p)) &&
                        !IsCommand(p) &&
                        !IsBindingProperty(p, analysis));

        foreach (PropertyDeclarationSyntax property in computedProperties)
        {
            List<string> dependencies = ExtractPropertiesFromProperty(property);

            ComputedPropertyInfo computedInfo = new ComputedPropertyInfo
            {
                PropertyName = property.Identifier.ValueText,
                Expression = property.ExpressionBody?.Expression,
                DirectDependencies = dependencies
            };

            analysis.ComputedProperties.Add(computedInfo);
        }
    }

    private void InventoryCanExecuteElements(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
    {
        // Métodos CanExecute
        IEnumerable<MethodDeclarationSyntax> canExecuteMethods = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText.StartsWith("Can") &&
                        m.ReturnType.ToString() == "bool");

        foreach (MethodDeclarationSyntax method in canExecuteMethods)
        {
            List<string> dependencies = ExtractPropertiesFromMethod(method);

            CanExecuteMethodInfo methodInfo = new CanExecuteMethodInfo
            {
                MethodName = method.Identifier.ValueText, DirectDependencies = dependencies
            };

            analysis.CanExecuteMethods.Add(methodInfo);
        }

        // Propiedades CanExecute
        IEnumerable<PropertyDeclarationSyntax> canExecuteProperties = classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Identifier.ValueText.StartsWith("Can") &&
                        p.Type.ToString() == "bool" &&
                        !IsCommand(p) &&
                        !IsBindingProperty(p, analysis));

        foreach (PropertyDeclarationSyntax property in canExecuteProperties)
        {
            List<string> dependencies = ExtractPropertiesFromProperty(property);

            CanExecuteMethodInfo methodInfo = new CanExecuteMethodInfo
            {
                MethodName = property.Identifier.ValueText, DirectDependencies = dependencies
            };

            analysis.CanExecuteMethods.Add(methodInfo);
        }
    }

    private void InventoryCommands(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
    {
        // Obtener todas las propiedades de comando
        IEnumerable<PropertyDeclarationSyntax> commandProperties = classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => IsCommand(p));

        foreach (PropertyDeclarationSyntax commandProperty in commandProperties)
        {
            string commandName = commandProperty.Identifier.ValueText;
            CommandInfo commandInfo = new CommandInfo
            {
                PropertyName = commandName,
                CanExecuteReferences = new List<string>(),
                DirectDependencies = new List<string>()
            };

            // CASO 1: Propiedades de expresión (expression-bodied properties)
            // public RelayCommand SaveMacroCommand => CreateCommand(SaveMacro, () => CanSaveMacro);
            if (commandProperty.ExpressionBody?.Expression != null)
            {
                AnalyzeCommandAssignment(commandProperty.ExpressionBody.Expression, commandInfo, analysis);
            }
            // CASO 2: Propiedades con inicializador
            // public RelayCommand SaveCommand { get; } = CreateCommand(...);
            else if (commandProperty.Initializer?.Value != null)
            {
                AnalyzeCommandAssignment(commandProperty.Initializer.Value, commandInfo, analysis);
            }
            // CASO 3: Propiedades con getter que retorna CreateCommand
            else if (commandProperty.AccessorList?.Accessors.Any() == true)
            {
                foreach (AccessorDeclarationSyntax accessor in commandProperty.AccessorList.Accessors)
                {
                    if (accessor.Keyword.ValueText == "get")
                    {
                        if (accessor.ExpressionBody?.Expression != null)
                        {
                            AnalyzeCommandAssignment(accessor.ExpressionBody.Expression, commandInfo, analysis);
                        }
                        else if (accessor.Body?.Statements.Any() == true)
                        {
                            // Buscar return statements en el getter
                            foreach (StatementSyntax statement in accessor.Body.Statements)
                            {
                                if (statement is ReturnStatementSyntax returnStatement &&
                                    returnStatement.Expression != null)
                                {
                                    AnalyzeCommandAssignment(returnStatement.Expression, commandInfo, analysis);
                                }
                            }
                        }
                    }
                }
            }

            analysis.Commands.Add(commandInfo);
        }

        // CASO 4: Comandos asignados en constructor (compatibilidad con TestViewModel)
        HashSet<string> existingCommandNames = new HashSet<string>(
            analysis.Commands.Select(c => c.PropertyName));

        IEnumerable<ConstructorDeclarationSyntax> constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>();

        foreach (ConstructorDeclarationSyntax constructor in constructors)
        {
            if (constructor.Body != null)
            {
                foreach (StatementSyntax statement in constructor.Body.Statements)
                {
                    AnalyzeConstructorStatement(statement, existingCommandNames, analysis);
                }
            }
        }
    }

    private void AnalyzeConstructorStatement(StatementSyntax statement, HashSet<string> existingCommandNames,
        ViewModelAnalysis analysis)
    {
        if (statement is ExpressionStatementSyntax exprStatement &&
            exprStatement.Expression is AssignmentExpressionSyntax assignment &&
            assignment.Left is IdentifierNameSyntax identifier)
        {
            string commandName = identifier.Identifier.ValueText;

            // Verificar si es un comando y si ya existe
            if (commandName.EndsWith("Command") && !existingCommandNames.Contains(commandName))
            {
                CommandInfo commandInfo = new CommandInfo
                {
                    PropertyName = commandName,
                    CanExecuteReferences = new List<string>(),
                    DirectDependencies = new List<string>()
                };

                // Analizar el lado derecho de la asignación
                AnalyzeCommandAssignment(assignment.Right, commandInfo, analysis);
                analysis.Commands.Add(commandInfo);
                existingCommandNames.Add(commandName);
            }
            // Si ya existe, analizar asignación adicional
            else if (existingCommandNames.Contains(commandName))
            {
                CommandInfo? existingCommand = analysis.Commands
                    .FirstOrDefault(c => c.PropertyName == commandName);
                if (existingCommand != null)
                {
                    AnalyzeCommandAssignment(assignment.Right, existingCommand, analysis);
                }
            }
        }
    }

    private void InventoryPartialVoidMethods(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
    {
        IEnumerable<MethodDeclarationSyntax> partialVoidMethods = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.ValueText == "partial") &&
                        m.ReturnType.ToString() == "void" &&
                        m.Identifier.ValueText.StartsWith("On") &&
                        m.Identifier.ValueText.EndsWith("Changed"));

        foreach (MethodDeclarationSyntax method in partialVoidMethods)
        {
            if (method.Body != null || method.ExpressionBody != null)
            {
                List<string> methodCalls = ExtractMethodCalls(method);
                SyntaxNode hookBody = (SyntaxNode?)method.Body ?? method.ExpressionBody!.Expression;

                PartialVoidMethodInfo partialMethodInfo = new PartialVoidMethodInfo
                {
                    MethodName = method.Identifier.ValueText,
                    CalledMethods = methodCalls,
                    PropertyName = ExtractPropertyNameFromOnChanged(method.Identifier.ValueText),
                    ModifiedCollections = CollectionsModifiedFrom(hookBody, new HashSet<string>()),
                    ManualNotifications = ManualNotificationsFrom(hookBody, new HashSet<string>())
                };

                analysis.PartialVoidMethods.Add(partialMethodInfo);
            }
        }
    }

    private void InventoryCollectionModifyingMethods(ClassDeclarationSyntax classDeclaration,
        ViewModelAnalysis analysis)
    {
        IEnumerable<MethodDeclarationSyntax> allMethods = classDeclaration.Members.OfType<MethodDeclarationSyntax>();

        foreach (MethodDeclarationSyntax method in allMethods)
        {
            SyntaxNode? body = method.Body ?? (SyntaxNode?)method.ExpressionBody?.Expression;
            if (body == null) continue;

            List<string> collectionModifications = DetectCollectionModifications(body);
            if (collectionModifications.Any())
            {
                CollectionModifyingMethodInfo methodInfo = new CollectionModifyingMethodInfo
                {
                    MethodName = method.Identifier.ValueText,
                    ModifiedCollections = collectionModifications,
                    ManualNotifications = ExtractManualNotifications(body)
                };

                analysis.CollectionModifyingMethods.Add(methodInfo);
            }
        }
    }

    #endregion

    #region Phase 2: Complete Dependency Graph

    private void BuildCompleteDependencyGraph(ViewModelAnalysis analysis)
    {
        // Construir mapa de todas las propiedades/métodos y sus dependencias directas
        Dictionary<string, HashSet<string>> directDependencies = new Dictionary<string, HashSet<string>>();

        // Agregar binding fields (sin dependencias)
        foreach (BindingFieldInfo binding in analysis.BindingFields)
        {
            directDependencies[binding.PropertyName] = new HashSet<string>();
        }

        // Agregar computed properties
        foreach (ComputedPropertyInfo computed in analysis.ComputedProperties)
        {
            directDependencies[computed.PropertyName] = new HashSet<string>(computed.DirectDependencies);
        }

        // Agregar CanExecute methods/properties
        foreach (CanExecuteMethodInfo canExecute in analysis.CanExecuteMethods)
        {
            directDependencies[canExecute.MethodName] = new HashSet<string>(canExecute.DirectDependencies);
        }

        // Resolver dependencias transitivas
        foreach (string property in directDependencies.Keys.ToList())
        {
            HashSet<string> allDependencies =
                ComputeTransitiveDependencies(property, directDependencies, new HashSet<string>());
            analysis.TransitiveDependencies[property] = allDependencies.ToList();
        }
    }

    private HashSet<string> ComputeTransitiveDependencies(string property,
        Dictionary<string, HashSet<string>> directDependencies, HashSet<string> visited)
    {
        if (visited.Contains(property))
            return new HashSet<string>(); // Evitar ciclos

        visited.Add(property);
        HashSet<string> result = new HashSet<string>();

        if (directDependencies.TryGetValue(property, out HashSet<string>? directDeps))
        {
            foreach (string directDep in directDeps)
            {
                result.Add(directDep);

                // Agregar dependencias transitivas
                HashSet<string> transitiveDeps = ComputeTransitiveDependencies(directDep, directDependencies, visited);
                result.UnionWith(transitiveDeps);
            }
        }

        visited.Remove(property);
        return result;
    }

    #endregion

    #region Phase 3: Command Dependencies Analysis

    private void AnalyzeCommandDependencies(ViewModelAnalysis analysis)
    {
        foreach (CommandInfo command in analysis.Commands)
        {
            HashSet<string> bindingDependencies = new HashSet<string>();

            // Procesar todas las referencias de CanExecute
            foreach (string canExecuteRef in command.CanExecuteReferences)
            {
                HashSet<string> dependencies = GetAllBindingDependencies(canExecuteRef, analysis);
                bindingDependencies.UnionWith(dependencies);
            }

            // Si no hay CanExecute explícito, analizar método execute para dependencias implícitas
            if (!command.CanExecuteReferences.Any())
            {
                AnalyzeExecuteMethodForImplicitDependencies(command, analysis, bindingDependencies);
            }

            command.DirectDependencies = bindingDependencies.ToList();
        }
    }

    private void AnalyzeExecuteMethodForImplicitDependencies(CommandInfo command, ViewModelAnalysis analysis,
        HashSet<string> bindingDependencies)
    {
        // Extraer el nombre del método base del comando
        string methodName = ExtractExecuteMethodName(command.PropertyName);

        // Buscar el método execute correspondiente
        MethodDeclarationSyntax? executeMethod = FindMethod(methodName, analysis);
        if (executeMethod != null)
        {
            // Analizar las propiedades que modifica el método
            HashSet<string> modifiedProperties = AnalyzeMethodForPropertyModifications(executeMethod);

            // Inferir dependencias basándose en las propiedades modificadas
            InferDependenciesFromModifications(modifiedProperties, bindingDependencies, analysis);

            // Agregar las dependencias inferidas al comando
            foreach (string dependency in bindingDependencies)
            {
                if (!command.DirectDependencies.Contains(dependency))
                {
                    command.DirectDependencies.Add(dependency);
                }
            }
        }
    }

    private string ExtractExecuteMethodName(string commandName)
    {
        if (commandName.EndsWith("Command"))
        {
            string baseName = commandName.Substring(0, commandName.Length - 7);

            // Probar diferentes patrones comunes
            string[] patterns = { baseName + "Async", baseName, "Execute" + baseName };

            foreach (string pattern in patterns)
            {
                MethodDeclarationSyntax? method = AllMembers()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == pattern);

                if (method != null)
                {
                    return pattern;
                }
            }
        }

        return commandName;
    }

    private void InferDependenciesFromModifications(HashSet<string> modifiedProperties,
        HashSet<string> bindingDependencies, ViewModelAnalysis analysis)
    {
        // Si modifica IsLoading, el comando típicamente no puede ejecutarse mientras está cargando
        if (modifiedProperties.Contains("IsLoading"))
        {
            bindingDependencies.Add("IsLoading");
        }

        // Si modifica propiedades de error, puede depender del estado de error
        if (modifiedProperties.Contains("HasErrors"))
        {
            bindingDependencies.Add("HasErrors");
        }

        // Si modifica colecciones, puede depender del estado de las colecciones
        foreach (string modifiedProp in modifiedProperties)
        {
            if (modifiedProp.Contains("Items") || modifiedProp.EndsWith("Collection"))
            {
                // Buscar computed properties que dependan de esta colección
                foreach (ComputedPropertyInfo computed in analysis.ComputedProperties)
                {
                    if (computed.DirectDependencies.Contains(modifiedProp))
                    {
                        bindingDependencies.Add(modifiedProp);
                        break;
                    }
                }
            }
        }
    }

    private HashSet<string> GetAllBindingDependencies(string element, ViewModelAnalysis analysis)
    {
        return GetAllBindingDependencies(element, analysis, new HashSet<string>());
    }

    private HashSet<string> GetAllBindingDependencies(string element, ViewModelAnalysis analysis, HashSet<string> visited)
    {
        HashSet<string> result = new HashSet<string>();

        // Evitar ciclos recursivos
        if (visited.Contains(element))
            return result;

        visited.Add(element);

        try
        {
            // Si el elemento es directamente una binding property
            if (analysis.BindingFields.Any(b => b.PropertyName == element))
            {
                result.Add(element);
                return result;
            }

            // Si el elemento es una computed property
            ComputedPropertyInfo? computedProp = analysis.ComputedProperties
                .FirstOrDefault(cp => cp.PropertyName == element);

            if (computedProp != null)
            {
                foreach (string directDep in computedProp.DirectDependencies)
                {
                    result.UnionWith(GetAllBindingDependencies(directDep, analysis, visited));
                }

                return result;
            }

            // Si el elemento es un método CanExecute
            CanExecuteMethodInfo? canExecuteMethod = analysis.CanExecuteMethods
                .FirstOrDefault(cem => cem.MethodName == element);

            if (canExecuteMethod != null)
            {
                foreach (string directDep in canExecuteMethod.DirectDependencies)
                {
                    result.UnionWith(GetAllBindingDependencies(directDep, analysis, visited));
                }

                return result;
            }

            return result;
        }
        finally
        {
            // IMPORTANTE: Remover el elemento del visited para permitir otros caminos de análisis
            visited.Remove(element);
        }
    }

    #endregion

    #region Phase 4: Notification Requirements

    private void CalculateNotificationRequirements(ViewModelAnalysis analysis)
    {
        foreach (BindingFieldInfo field in analysis.BindingFields)
        {
            if (field.ReadOnly) continue;

            NotificationRequirements requirements = new NotificationRequirements
            {
                PropertyName = field.PropertyName,
                ComputedPropertyNotifications = GetComputedPropertyNotifications(field.PropertyName, analysis),
                CommandNotifications = GetCommandNotifications(field.PropertyName, analysis),
                CollectionDependentNotifications = GetCollectionDependentNotifications(field.PropertyName, analysis)
            };

            analysis.NotificationRequirements[field.PropertyName] = requirements;
        }
    }

    private List<string> GetComputedPropertyNotifications(string propertyName, ViewModelAnalysis analysis)
    {
        HashSet<string> notifications = new HashSet<string>();

        // Notificaciones directas - computed properties que dependen directamente de esta propiedad
        foreach (ComputedPropertyInfo computed in analysis.ComputedProperties)
        {
            if (computed.DirectDependencies.Contains(propertyName))
            {
                notifications.Add(computed.PropertyName);
            }
        }

        // Notificaciones transitivas - computed properties que dependen transitivamente
        foreach (KeyValuePair<string, List<string>> kvp in analysis.TransitiveDependencies)
        {
            if (kvp.Value.Contains(propertyName))
            {
                ComputedPropertyInfo? computed = analysis.ComputedProperties
                    .FirstOrDefault(cp => cp.PropertyName == kvp.Key);
                if (computed != null)
                {
                    notifications.Add(computed.PropertyName);
                }
            }
        }

        return notifications.ToList();
    }

    private List<string> GetCommandNotifications(string propertyName, ViewModelAnalysis analysis)
    {
        HashSet<string> notifications = new HashSet<string>();

        foreach (CommandInfo command in analysis.Commands)
        {
            bool shouldNotify = false;

            // CASO 1: Dependencia directa - el comando depende directamente de la propiedad
            if (command.DirectDependencies.Contains(propertyName))
            {
                shouldNotify = true;
            }

            // CASO 2: Dependencia a través de computed properties o CanExecute methods
            if (!shouldNotify)
            {
                foreach (string canExecuteRef in command.CanExecuteReferences)
                {
                    // Verificar si el CanExecute es una computed property que depende de esta propiedad
                    ComputedPropertyInfo? computedProp = analysis.ComputedProperties
                        .FirstOrDefault(cp => cp.PropertyName == canExecuteRef);

                    if (computedProp != null && computedProp.DirectDependencies.Contains(propertyName))
                    {
                        shouldNotify = true;
                        break;
                    }

                    // Verificar si el CanExecute es un método que depende de esta propiedad
                    CanExecuteMethodInfo? canExecuteMethod = analysis.CanExecuteMethods
                        .FirstOrDefault(cem => cem.MethodName == canExecuteRef);

                    if (canExecuteMethod != null && canExecuteMethod.DirectDependencies.Contains(propertyName))
                    {
                        shouldNotify = true;
                        break;
                    }

                    // Verificar dependencias transitivas
                    if (analysis.TransitiveDependencies.TryGetValue(canExecuteRef, out List<string>? transitiveDeps) &&
                        transitiveDeps.Contains(propertyName))
                    {
                        shouldNotify = true;
                        break;
                    }
                }
            }

            if (shouldNotify)
            {
                notifications.Add(command.PropertyName);
            }
        }

        return notifications.ToList();
    }

    private List<string> GetCollectionDependentNotifications(string propertyName, ViewModelAnalysis analysis)
    {
        List<string> result = new List<string>();

        PartialVoidMethodInfo? partialMethod = analysis.PartialVoidMethods
            .FirstOrDefault(pvm => pvm.PropertyName == propertyName);

        if (partialMethod != null)
        {
            // Everything the hook reaches, in its own body or through what it calls.
            result.AddRange(partialMethod.ManualNotifications);

            foreach (string modifiedCollection in partialMethod.ModifiedCollections)
            {
                result.AddRange(analysis.ComputedProperties
                    .Where(cp => cp.DirectDependencies.Contains(modifiedCollection))
                    .Select(cp => cp.PropertyName));
            }
        }

        return result.Distinct().ToList();
    }

    #endregion

    #region Command Assignment Analysis

    private void AnalyzeCommandAssignment(ExpressionSyntax assignmentExpression, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        if (assignmentExpression is InvocationExpressionSyntax invocation)
        {
            // Casos como CreateCommand(ExecuteMethod, CanExecuteMethod) o CreateCommand<T>(...)
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "CreateCommand")
            {
                ArgumentListSyntax arguments = invocation.ArgumentList;

                if (arguments.Arguments.Count >= 2)
                {
                    // Hay al menos 2 argumentos: ExecuteMethod y CanExecute
                    ExpressionSyntax canExecuteArg = arguments.Arguments[1].Expression;
                    AnalyzeCanExecuteArgument(canExecuteArg, commandInfo, analysis);
                }
                else if (arguments.Arguments.Count == 1)
                {
                    // Solo ExecuteMethod - comando siempre habilitado
                    // No hay dependencias que agregar
                }
            }
            // CASO ADICIONAL: new RelayCommand(execute, canExecute)
            else if (invocation.Expression is IdentifierNameSyntax constructor &&
                     (constructor.Identifier.ValueText == "RelayCommand" ||
                      constructor.Identifier.ValueText.Contains("Command")))
            {
                ArgumentListSyntax arguments = invocation.ArgumentList;
                if (arguments.Arguments.Count >= 2)
                {
                    ExpressionSyntax canExecuteArg = arguments.Arguments[1].Expression;
                    AnalyzeCanExecuteArgument(canExecuteArg, commandInfo, analysis);
                }
            }
        }
    }

    private void AnalyzeCanExecuteArgument(ExpressionSyntax canExecuteArg, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        switch (canExecuteArg)
        {
            // Caso: () => CanSaveMacro, () => true, () => CanRecord || CanStopRecording
            case ParenthesizedLambdaExpressionSyntax lambda:
                if (lambda.ExpressionBody != null)
                {
                    AnalyzeLambdaExpression(lambda.ExpressionBody, commandInfo, analysis);
                }
                else if (lambda.Block != null)
                {
                    AnalyzeLambdaBlock(lambda.Block, commandInfo, analysis);
                }

                break;

            // Caso: item => !string.IsNullOrEmpty(item) && !IsLoading
            case SimpleLambdaExpressionSyntax simpleLambda:
                if (simpleLambda.ExpressionBody != null)
                {
                    AnalyzeLambdaExpression(simpleLambda.ExpressionBody, commandInfo, analysis);
                }
                else if (simpleLambda.Block != null)
                {
                    AnalyzeLambdaBlock(simpleLambda.Block, commandInfo, analysis);
                }

                break;

            // Caso: Referencia directa a método/propiedad - CanExecuteComplexOperation, CanSaveMacro
            case IdentifierNameSyntax identifier:
                string referenceName = identifier.Identifier.ValueText;
                if (!commandInfo.CanExecuteReferences.Contains(referenceName))
                {
                    commandInfo.CanExecuteReferences.Add(referenceName);
                }

                break;

            // Caso: Literal true/false
            case LiteralExpressionSyntax:
                // Para casos como () => true, no hay dependencias
                break;
        }
    }

    private void AnalyzeLambdaExpression(ExpressionSyntax expression, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        switch (expression)
        {
            // Caso simple: CanSaveMacro, IsLoading, CanExecuteMacro
            case IdentifierNameSyntax identifier:
                string identifierName = identifier.Identifier.ValueText;
                if (!commandInfo.CanExecuteReferences.Contains(identifierName))
                {
                    commandInfo.CanExecuteReferences.Add(identifierName);
                }

                break;

            // Caso complejo: CanRecord || CanStopRecording, !IsLoading && IsEnabled
            case BinaryExpressionSyntax binaryExpression:
                AnalyzeBinaryExpression(binaryExpression, commandInfo, analysis);
                break;

            // Caso: !IsLoading, !HasErrors
            case PrefixUnaryExpressionSyntax prefixUnary:
                AnalyzeLambdaExpression(prefixUnary.Operand, commandInfo, analysis);
                break;

            // Caso: string.IsNullOrEmpty(item), HasRecordedActions
            case InvocationExpressionSyntax invocation:
                AnalyzeInvocationInLambda(invocation, commandInfo, analysis);
                break;

            // Caso: Items.Count, RecordedActions.Count > 0
            case MemberAccessExpressionSyntax memberAccess:
                AnalyzeMemberAccessInLambda(memberAccess, commandInfo, analysis);
                break;

            // Caso: Literal true/false, números, strings
            case LiteralExpressionSyntax:
                // No hay dependencias para literales
                break;

            // Caso: Expresiones condicionales ?: 
            case ConditionalExpressionSyntax conditionalExpression:
                AnalyzeLambdaExpression(conditionalExpression.Condition, commandInfo, analysis);
                AnalyzeLambdaExpression(conditionalExpression.WhenTrue, commandInfo, analysis);
                AnalyzeLambdaExpression(conditionalExpression.WhenFalse, commandInfo, analysis);
                break;

            // Caso: Paréntesis (CanSaveMacro)
            case ParenthesizedExpressionSyntax parenthesized:
                AnalyzeLambdaExpression(parenthesized.Expression, commandInfo, analysis);
                break;
        }
    }

    private void AnalyzeInvocationInLambda(InvocationExpressionSyntax invocation, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Analizar argumentos de la invocación
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            AnalyzeLambdaExpression(argument.Expression, commandInfo, analysis);
        }

        // Analizar la expresión de la invocación
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            AnalyzeMemberAccessInLambda(memberAccess, commandInfo, analysis);
        }
        else if (invocation.Expression is IdentifierNameSyntax methodIdentifier)
        {
            // Para métodos directos como CanExecuteComplexOperation()
            string methodName = methodIdentifier.Identifier.ValueText;
            if (!commandInfo.CanExecuteReferences.Contains(methodName))
            {
                commandInfo.CanExecuteReferences.Add(methodName);
            }
        }
    }


    private void AnalyzeMemberAccessInLambda(MemberAccessExpressionSyntax memberAccess, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Para casos como Items.Count, Email.Contains, RecordedActions.Count
        if (memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            string propertyName = identifier.Identifier.ValueText;

            // Solo agregar si es una propiedad conocida del ViewModel
            if (IsKnownViewModelProperty(propertyName, analysis))
            {
                if (!commandInfo.CanExecuteReferences.Contains(propertyName))
                {
                    commandInfo.CanExecuteReferences.Add(propertyName);
                }
            }
        }
    }

    private void AnalyzeBinaryExpression(BinaryExpressionSyntax binaryExpression, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Analizar AMBOS lados de la expresión binaria
        AnalyzeLambdaExpression(binaryExpression.Left, commandInfo, analysis);
        AnalyzeLambdaExpression(binaryExpression.Right, commandInfo, analysis);
    }

    private void AnalyzeLambdaBlock(BlockSyntax block, CommandInfo commandInfo, ViewModelAnalysis analysis)
    {
        // Para lambdas con cuerpo de bloque { ... }
        foreach (StatementSyntax statement in block.Statements)
        {
            // Analizar todas las expresiones dentro del bloque
            foreach (SyntaxNode node in statement.DescendantNodesAndSelf())
            {
                if (node is IdentifierNameSyntax identifier)
                {
                    string identifierName = identifier.Identifier.ValueText;
                    if (IsKnownViewModelProperty(identifierName, analysis) &&
                        !commandInfo.CanExecuteReferences.Contains(identifierName))
                    {
                        commandInfo.CanExecuteReferences.Add(identifierName);
                    }
                }
            }
        }
    }

    private bool IsKnownViewModelProperty(string propertyName, ViewModelAnalysis analysis)
    {
        return analysis.BindingFields.Any(bf => bf.PropertyName == propertyName) ||
               analysis.ComputedProperties.Any(cp => cp.PropertyName == propertyName) ||
               analysis.CanExecuteMethods.Any(cem => cem.MethodName == propertyName);
    }

    #endregion

    #region Helper Methods

    private bool HasBindingAttribute(FieldDeclarationSyntax field)
    {
        return field.AttributeLists.Any(al =>
            al.Attributes.Any(a => a.Name.ToString().Contains("Binding")));
    }

    private bool IsCommand(PropertyDeclarationSyntax property)
    {
        string typeName = property.Type.ToString();
        return typeName.Contains("RelayCommand") ||
               typeName.Contains("ICommand") ||
               typeName.EndsWith("Command");
    }

    private bool IsBindingProperty(PropertyDeclarationSyntax property, ViewModelAnalysis analysis)
    {
        return analysis.BindingFields.Any(bf => bf.PropertyName == property.Identifier.ValueText);
    }

    private bool HasComputedPropertyBody(PropertyDeclarationSyntax property)
    {
        return property.AccessorList?.Accessors.Any(a =>
            a.Keyword.ValueText == "get" &&
            (a.Body != null || a.ExpressionBody != null)) == true;
    }

    private List<string> ExtractPropertiesFromProperty(PropertyDeclarationSyntax property)
    {
        List<string> properties = new List<string>();

        if (property.ExpressionBody != null)
        {
            properties.AddRange(ExtractIdentifiersFromExpression(property.ExpressionBody.Expression));
        }
        else if (property.AccessorList != null)
        {
            AccessorDeclarationSyntax? getter = property.AccessorList.Accessors
                .FirstOrDefault(a => a.Keyword.ValueText == "get");

            if (getter != null)
            {
                properties.AddRange(ExtractPropertiesFromAccessor(getter));
            }
        }

        // Through the methods it calls, too: a computed property written as Total => Compute() reads
        // its dependencies just as surely as one written inline, and commands and collections have
        // always been followed this way.
        SyntaxNode? body = property.ExpressionBody?.Expression
                           ?? (SyntaxNode?)property.AccessorList?.Accessors
                               .FirstOrDefault(a => a.Keyword.ValueText == "get");

        if (body != null)
        {
            properties.AddRange(DependenciesThroughCalls(body, new HashSet<string>()));
        }

        return properties.Where(p => IsValidPropertyReference(p)).Distinct().ToList();
    }

    /// <summary>
    /// Collections modified starting from <paramref name="body"/>, following what it calls.
    /// </summary>
    /// <remarks>
    /// A change hook mutates a collection directly as often as it delegates, and a helper may call
    /// another helper. Only the one-hop delegating shape used to be recognised, so the other ways
    /// of writing the same thing silently notified nothing.
    /// </remarks>
    private List<string> CollectionsModifiedFrom(SyntaxNode body, HashSet<string> visitedMethods) =>
        WalkFrom(body, visitedMethods, DetectCollectionModifications);

    private List<string> ManualNotificationsFrom(SyntaxNode body, HashSet<string> visitedMethods) =>
        WalkFrom(body, visitedMethods, ExtractManualNotifications);

    /// <summary>Applies <paramref name="collect"/> to a body and to every method it reaches.</summary>
    private List<string> WalkFrom(SyntaxNode body, HashSet<string> visitedMethods,
        Func<SyntaxNode, List<string>> collect)
    {
        List<string> found = new List<string>(collect(body));

        foreach (MethodDeclarationSyntax method in MethodsCalledFrom(body, visitedMethods))
        {
            SyntaxNode? methodBody = (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression;

            if (methodBody == null) continue;

            found.AddRange(WalkFrom(methodBody, visitedMethods, collect));
        }

        return found.Distinct().ToList();
    }

    /// <summary>Methods invoked in <paramref name="body"/> that this class declares.</summary>
    private IEnumerable<MethodDeclarationSyntax> MethodsCalledFrom(SyntaxNode body, HashSet<string> visitedMethods)
    {
        // DescendantNodesAndSelf: an expression-bodied member is the invocation, not its parent.
        foreach (InvocationExpressionSyntax invocation in body.DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            string? methodName = invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access =>
                    access.Name.Identifier.ValueText,
                _ => null
            };

            // The visited set also stops a method that calls itself from looping.
            if (methodName == null || !visitedMethods.Add(methodName)) continue;

            MethodDeclarationSyntax? method = AllMembers()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Identifier.ValueText == methodName);

            if (method != null) yield return method;
        }
    }

    /// <summary>Identifiers read by the methods invoked inside <paramref name="body"/>, transitively.</summary>
    private List<string> DependenciesThroughCalls(SyntaxNode body, HashSet<string> visitedMethods)
    {
        List<string> found = new List<string>();

        // DescendantNodesAndSelf: an expression-bodied property is the invocation, not its parent.
        foreach (InvocationExpressionSyntax invocation in body.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            string? methodName = invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access =>
                    access.Name.Identifier.ValueText,
                _ => null
            };

            // The visited set also stops a method that calls itself from looping.
            if (methodName == null || !visitedMethods.Add(methodName)) continue;

            MethodDeclarationSyntax? method = AllMembers()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(candidate => candidate.Identifier.ValueText == methodName);

            SyntaxNode? methodBody = (SyntaxNode?)method?.Body ?? method?.ExpressionBody?.Expression;

            if (methodBody == null) continue;

            found.AddRange(methodBody.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => identifier.Identifier.ValueText));

            found.AddRange(DependenciesThroughCalls(methodBody, visitedMethods));
        }

        return found;
    }

    private List<string> ExtractPropertiesFromMethod(MethodDeclarationSyntax method)
    {
        List<string> properties = new List<string>();

        if (method.Body != null)
        {
            properties.AddRange(ExtractIdentifiersFromStatement(method.Body));
        }
        else if (method.ExpressionBody != null)
        {
            properties.AddRange(ExtractIdentifiersFromExpression(method.ExpressionBody.Expression));
        }

        return properties.Where(p => IsValidPropertyReference(p)).Distinct().ToList();
    }

    private List<string> ExtractPropertiesFromAccessor(AccessorDeclarationSyntax accessor)
    {
        List<string> properties = new List<string>();

        if (accessor.Body != null)
        {
            properties.AddRange(ExtractIdentifiersFromStatement(accessor.Body));
        }
        else if (accessor.ExpressionBody != null)
        {
            properties.AddRange(ExtractIdentifiersFromExpression(accessor.ExpressionBody.Expression));
        }

        return properties;
    }

    private List<string> ExtractIdentifiersFromExpression(ExpressionSyntax expression)
    {
        List<string> identifiers = new List<string>();

        // Usar DescendantNodesAndSelf para obtener TODOS los nodos
        foreach (SyntaxNode node in expression.DescendantNodesAndSelf())
        {
            if (node is IdentifierNameSyntax identifier)
            {
                string name = identifier.Identifier.ValueText;
                if (IsValidPropertyReference(name) && !IsMethodCall(node))
                {
                    identifiers.Add(name);
                }
            }
        }

        return identifiers.Distinct().ToList();
    }

    private List<string> ExtractIdentifiersFromStatement(BlockSyntax block)
    {
        List<string> identifiers = new List<string>();

        foreach (SyntaxNode node in block.DescendantNodesAndSelf())
        {
            if (node is IdentifierNameSyntax identifier)
            {
                string name = identifier.Identifier.ValueText;
                if (IsValidPropertyReference(name) && !IsMethodCall(node))
                {
                    identifiers.Add(name);
                }
            }
        }

        return identifiers.Distinct().ToList();
    }

    private bool IsValidPropertyReference(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               char.IsUpper(name[0]) &&
               !IsSystemType(name) &&
               !IsKeyword(name);
    }

    private bool IsMethodCall(SyntaxNode node)
    {
        return node.Parent is InvocationExpressionSyntax ||
               (node.Parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name == node &&
                memberAccess.Parent is InvocationExpressionSyntax);
    }

    private bool IsSystemType(string name)
    {
        return name is "String" or "Int32" or "Boolean" or "Object" or "DateTime" or "Task" or
            "Count" or "Length" or "Empty" or "True" or "False" or "Guid";
    }

    private bool IsKeyword(string name)
    {
        return name is "true" or "false" or "null" or "this" or "base" or "return" or "if" or "else";
    }

    private List<string> ExtractMethodCalls(MethodDeclarationSyntax method)
    {
        List<string> methodCalls = new List<string>();
        SyntaxNode? body = method.Body ?? (SyntaxNode?)method.ExpressionBody?.Expression;
        if (body == null) return methodCalls;

        // DescendantNodesAndSelf: for expression-bodied hooks (=> ApplyFilter();) the invocation
        // is the body itself, so DescendantNodes alone would miss it.
        foreach (SyntaxNode node in body.DescendantNodesAndSelf())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                string? methodName = ExtractMethodName(invocation.Expression);
                if (methodName is { Length: > 0 })
                {
                    methodCalls.Add(methodName);
                }
            }
        }

        return methodCalls.Distinct().ToList();
    }

    private string? ExtractMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null
        };
    }

    private string ExtractPropertyNameFromOnChanged(string methodName)
    {
        if (methodName.StartsWith("On") && methodName.EndsWith("Changed"))
        {
            return methodName.Substring(2, methodName.Length - 9);
        }

        return methodName;
    }

    private List<string> DetectCollectionModifications(SyntaxNode methodBody)
    {
        List<string> modifications = new List<string>();

        foreach (SyntaxNode node in methodBody.DescendantNodesAndSelf())
        {
            if (node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                string methodName = memberAccess.Name.Identifier.ValueText;
                if (IsCollectionModificationMethod(methodName))
                {
                    if (memberAccess.Expression is IdentifierNameSyntax collectionIdentifier)
                    {
                        modifications.Add(collectionIdentifier.Identifier.ValueText);
                    }
                }
            }
        }

        return modifications.Distinct().ToList();
    }

    private bool IsCollectionModificationMethod(string methodName)
    {
        return methodName is "Add" or "Remove" or "Clear" or "Insert" or "RemoveAt" or
            "AddRange" or "RemoveRange" or "Sort" or "Reverse";
    }

    private List<string> ExtractManualNotifications(SyntaxNode methodBody)
    {
        List<string> notifications = new List<string>();

        foreach (SyntaxNode node in methodBody.DescendantNodesAndSelf())
        {
            // Matches both OnPropertyChanged(...) and this.OnPropertyChanged(...)
            if (node is not InvocationExpressionSyntax invocation ||
                ExtractMethodName(invocation.Expression) != "OnPropertyChanged")
            {
                continue;
            }

            ArgumentSyntax? arg = invocation.ArgumentList.Arguments.FirstOrDefault();

            // OnPropertyChanged(nameof(Property))
            if (arg?.Expression is InvocationExpressionSyntax nameofCall &&
                nameofCall.Expression is IdentifierNameSyntax nameofId &&
                nameofId.Identifier.ValueText == "nameof")
            {
                ArgumentSyntax? nameofArg = nameofCall.ArgumentList.Arguments.FirstOrDefault();
                if (nameofArg?.Expression is IdentifierNameSyntax propertyName)
                {
                    notifications.Add(propertyName.Identifier.ValueText);
                }
            }
            // OnPropertyChanged("Property")
            else if (arg?.Expression is LiteralExpressionSyntax literal &&
                     literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                string? value = literal.Token.ValueText;
                if (!string.IsNullOrEmpty(value))
                {
                    notifications.Add(value);
                }
            }
        }

        return notifications.Distinct().ToList();
    }

    private string GetNamespace(ClassDeclarationSyntax classDeclaration)
    {
        SyntaxNode? parent = classDeclaration.Parent;
        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax namespaceDecl)
                return namespaceDecl.Name.ToString();
            parent = parent.Parent;
        }

        return "";
    }

    /// <summary>
    /// Everything the generator can tell the author before it writes a line.
    /// </summary>
    /// <remarks>
    /// Each of these used to surface as a compiler error inside the generated file - about
    /// SetProperty, or a duplicate member - naming code the author never wrote.
    /// </remarks>
    private void Validate(IReadOnlyList<ViewModelPart> parts, ViewModelAnalysis analysis)
    {
        if (analysis.BindingFields.Count == 0) return;

        ViewModelPart primary = parts[0];
        string className = primary.Declaration.Identifier.ValueText;

        // Reported against the declaration that is missing the modifier, not the first one.
        foreach (ViewModelPart part in parts.Where(p => !p.Declaration.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            analysis.Diagnostics.Add(Diagnostic.Create(
                BindingDiagnostics.ClassMustBePartial, part.Declaration.Identifier.GetLocation(), className));
        }

        INamedTypeSymbol? classSymbol = primary.SemanticModel.GetDeclaredSymbol(primary.Declaration);

        if (classSymbol != null && !InheritsChangeNotificationMembers(classSymbol))
        {
            analysis.Diagnostics.Add(Diagnostic.Create(
                BindingDiagnostics.ClassMustDeriveFromViewModelBase,
                primary.Declaration.Identifier.GetLocation(), className));
        }

        foreach (ViewModelPart part in parts)
        {
            ValidateStaticFields(part.Declaration, part.SemanticModel, analysis);
        }

        ValidateGeneratedNames(parts, classSymbol, analysis);
    }

    /// <summary>
    /// Computed properties and commands that depend on a property inherited from a base view model.
    /// </summary>
    /// <remarks>
    /// The setter that raises such a property lives in the base class, which cannot know what a
    /// subclass derived from it, so nothing here can be emitted into that setter. The subclass
    /// overrides OnPropertyChanged and forwards instead.
    /// </remarks>
    private void CalculateInheritedDependencies(IReadOnlyList<ViewModelPart> parts, ViewModelAnalysis analysis)
    {
        ViewModelPart primary = parts[0];

        if (primary.SemanticModel.GetDeclaredSymbol(primary.Declaration) is not INamedTypeSymbol classSymbol)
        {
            return;
        }

        HashSet<string> declaredHere = new HashSet<string>(analysis.BindingFields.Select(f => f.PropertyName));

        foreach (ComputedPropertyInfo computed in analysis.ComputedProperties)
        {
            foreach (string dependency in computed.DirectDependencies.Distinct())
            {
                if (!IsInheritedDependency(dependency, declaredHere, classSymbol)) continue;

                Record(analysis.InheritedDependencyNotifications, dependency, computed.PropertyName);
            }
        }

        // A command reads its CanExecute the same way, and a base class cannot raise it either.
        foreach (CommandInfo command in analysis.Commands)
        {
            IEnumerable<string> references = command.DirectDependencies.Concat(command.CanExecuteReferences);

            foreach (string dependency in references.Distinct())
            {
                if (!IsInheritedDependency(dependency, declaredHere, classSymbol)) continue;

                Record(analysis.InheritedCommandNotifications, dependency, command.PropertyName);
            }
        }
    }

    private bool IsInheritedDependency(string dependency, HashSet<string> declaredHere,
        INamedTypeSymbol classSymbol) =>
        !declaredHere.Contains(dependency) && IsInheritedProperty(classSymbol, dependency);

    private static void Record(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out List<string>? entries))
        {
            entries = new List<string>();
            map[key] = entries;
        }

        if (!entries.Contains(value)) entries.Add(value);
    }

    /// <summary>
    /// Whether <paramref name="propertyName"/> comes from a base class, declared or generated.
    /// </summary>
    /// <remarks>
    /// A base view model's binding properties do not exist as symbols while the generator runs -
    /// they are what it is about to produce - so asking the base type for a property of that name
    /// finds nothing. Its [Binding] fields are visible, and the property they will produce is
    /// derived from them by the same rule used to generate it.
    /// </remarks>
    private bool IsInheritedProperty(INamedTypeSymbol classSymbol, string propertyName)
    {
        for (INamedTypeSymbol? current = classSymbol.BaseType; current != null; current = current.BaseType)
        {
            if (current.GetMembers(propertyName).OfType<IPropertySymbol>().Any()) return true;

            foreach (IFieldSymbol field in current.GetMembers().OfType<IFieldSymbol>())
            {
                AttributeData? binding = field.GetAttributes()
                    .FirstOrDefault(attribute => attribute.AttributeClass?.Name == "BindingAttribute");

                if (binding == null) continue;

                (string? custom, bool _) = GetBindingConfiguration(binding);

                if ((custom ?? GeneratePropertyName(field.Name)) == propertyName) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a base class supplies the two members generated properties call.
    /// </summary>
    /// <remarks>
    /// Asked as "does it have these members", not "is it ViewModelBase". An application with its
    /// own base implementing INotifyPropertyChanged works perfectly well with the generator, and
    /// rejecting it by name would break a build that was compiling fine.
    /// </remarks>
    private static bool InheritsChangeNotificationMembers(INamedTypeSymbol classSymbol)
    {
        bool setProperty = false;
        bool onPropertyChanged = false;

        for (INamedTypeSymbol? current = classSymbol.BaseType; current != null; current = current.BaseType)
        {
            setProperty |= current.GetMembers("SetProperty").OfType<IMethodSymbol>().Any();
            onPropertyChanged |= current.GetMembers("OnPropertyChanged").OfType<IMethodSymbol>().Any();

            if (setProperty && onPropertyChanged) return true;
        }

        return false;
    }

    private void ValidateStaticFields(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel,
        ViewModelAnalysis analysis)
    {
        foreach (FieldDeclarationSyntax field in classDeclaration.Members.OfType<FieldDeclarationSyntax>()
                     .Where(HasBindingAttribute))
        {
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                if (semanticModel.GetDeclaredSymbol(variable) is IFieldSymbol { IsStatic: true } staticField)
                {
                    analysis.Diagnostics.Add(Diagnostic.Create(
                        BindingDiagnostics.StaticFieldNotSupported,
                        variable.Identifier.GetLocation(),
                        staticField.Name));
                }
            }
        }
    }

    /// <summary>Names the generator is about to introduce, against each other and against the class.</summary>
    private void ValidateGeneratedNames(IReadOnlyList<ViewModelPart> parts, INamedTypeSymbol? classSymbol,
        ViewModelAnalysis analysis)
    {
        HashSet<string> seen = new HashSet<string>();

        foreach (BindingFieldInfo field in analysis.BindingFields)
        {
            Location location = FindFieldLocation(parts, field.FieldName);

            if (!seen.Add(field.PropertyName))
            {
                analysis.Diagnostics.Add(Diagnostic.Create(
                    BindingDiagnostics.DuplicatePropertyName, location, field.FieldName, field.PropertyName));
                continue;
            }

            bool alreadyDeclared = classSymbol != null &&
                                   classSymbol.GetMembers(field.PropertyName).Length > 0;

            if (alreadyDeclared)
            {
                analysis.Diagnostics.Add(Diagnostic.Create(
                    BindingDiagnostics.PropertyNameAlreadyTaken, location, field.FieldName, field.PropertyName));
                continue;
            }

            // Hiding compiles, so this is a warning - but the compiler would report it against the
            // generated file, and the member being hidden may be one the toolkit relies on.
            if (classSymbol != null && FindHidingBase(classSymbol, field.PropertyName) is { } hidden)
            {
                analysis.Diagnostics.Add(Diagnostic.Create(
                    BindingDiagnostics.PropertyHidesInheritedMember,
                    location, field.FieldName, field.PropertyName, hidden.Name));
            }
        }
    }

    private static INamedTypeSymbol? FindHidingBase(INamedTypeSymbol classSymbol, string propertyName)
    {
        for (INamedTypeSymbol? current = classSymbol.BaseType; current != null; current = current.BaseType)
        {
            if (current.GetMembers(propertyName).Length > 0) return current;
        }

        return null;
    }

    private static Location FindFieldLocation(IReadOnlyList<ViewModelPart> parts, string fieldName)
    {
        VariableDeclaratorSyntax? declarator = parts
            .SelectMany(part => part.Declaration.Members.OfType<FieldDeclarationSyntax>())
            .SelectMany(field => field.Declaration.Variables)
            .FirstOrDefault(variable => variable.Identifier.ValueText == fieldName);

        return declarator?.Identifier.GetLocation() ?? parts[0].Declaration.Identifier.GetLocation();
    }

    /// <summary>
    /// The types this one is nested inside, outermost first, each with its type parameters.
    /// </summary>
    /// <remarks>
    /// A nested view model has to be re-declared inside its containers or the generated partial
    /// describes a different, top-level type of the same name.
    /// </remarks>
    private List<string> GetContainingTypes(ClassDeclarationSyntax classDeclaration)
    {
        List<string> containers = new List<string>();

        for (SyntaxNode? parent = classDeclaration.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is TypeDeclarationSyntax type)
            {
                containers.Insert(0, type.Identifier.ValueText + (type.TypeParameterList?.ToString() ?? ""));
            }
        }

        return containers;
    }

    private BindingFieldInfo ExtractBindingInfo(IFieldSymbol fieldSymbol)
    {
        AttributeData? attribute = fieldSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "BindingAttribute");

        (string? customPropertyName, bool readOnly) = GetBindingConfiguration(attribute);
        string propertyName = customPropertyName ?? GeneratePropertyName(fieldSymbol.Name);
        List<string> validationAttributes = ExtractValidationAttributes(fieldSymbol);

        return new BindingFieldInfo
        {
            ClassName = fieldSymbol.ContainingType.Name,
            Namespace = fieldSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
            FieldName = fieldSymbol.Name,
            FieldType = fieldSymbol.Type.ToDisplayString(),
            PropertyName = propertyName,
            ReadOnly = readOnly,
            ValidationAttributes = validationAttributes
        };
    }

    /// <summary>
    /// Validation attributes on a field, as they were written, ready to be re-emitted.
    /// </summary>
    /// <remarks>
    /// Recognised by deriving from ValidationAttribute rather than by a list of known names, so an
    /// application's own rules travel to the generated property exactly like the built-in ones.
    /// The original source is reused so that arguments, named arguments and constants survive
    /// without the generator having to render them back.
    /// </remarks>
    private List<string> ExtractValidationAttributes(IFieldSymbol fieldSymbol)
    {
        List<string> attributes = new List<string>();

        foreach (AttributeData attribute in fieldSymbol.GetAttributes())
        {
            if (!IsValidationAttribute(attribute.AttributeClass)) continue;

            if (attribute.AttributeClass == null) continue;

            // Fully qualified: the generated file does not share the using directives of the file
            // the field was written in, and a short name can mean something else there entirely -
            // [Range] would bind to System.Range.
            string typeName = "global::" + attribute.AttributeClass.ToDisplayString();

            string arguments = attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax
                ? syntax.ArgumentList?.ToString() ?? ""
                : "";

            attributes.Add(typeName + arguments);
        }

        return attributes;
    }

    private static bool IsValidationAttribute(INamedTypeSymbol? attributeClass)
    {
        for (INamedTypeSymbol? current = attributeClass; current != null; current = current.BaseType)
        {
            if (current.Name == "ValidationAttribute") return true;
        }

        return false;
    }

    private (string? propertyName, bool readOnly) GetBindingConfiguration(AttributeData? attribute)
    {
        if (attribute == null) return (null, false);

        string? propertyName = null;
        bool readOnly = false;

        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "PropertyName":
                    propertyName = namedArg.Value.Value?.ToString();
                    break;
                case "ReadOnly":
                    readOnly = namedArg.Value.Value is true;
                    break;
            }
        }

        return (propertyName, readOnly);
    }

    private string GeneratePropertyName(string fieldName)
    {
        string baseName = fieldName.StartsWith("_") ? fieldName.Substring(1) : fieldName;
        return baseName.Length > 0 ? char.ToUpperInvariant(baseName[0]) + baseName.Substring(1) : baseName;
    }

    private MethodDeclarationSyntax? FindMethod(string methodName, ViewModelAnalysis analysis)
    {
        return AllMembers()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
    }

    private IEnumerable<MemberDeclarationSyntax> AllMembers() =>
        _declarations.SelectMany(declaration => declaration.Members);

    private HashSet<string> AnalyzeMethodForPropertyModifications(MethodDeclarationSyntax method)
    {
        HashSet<string> modifiedProperties = new HashSet<string>();

        if (method.Body != null)
        {
            foreach (SyntaxNode node in method.Body.DescendantNodes())
            {
                // Buscar asignaciones como IsLoading = true;
                if (node is AssignmentExpressionSyntax assignment &&
                    assignment.Left is IdentifierNameSyntax identifier)
                {
                    modifiedProperties.Add(identifier.Identifier.ValueText);
                }
            }
        }

        return modifiedProperties;
    }

    #endregion
}

#region Enhanced Analysis Data Structures

/// <summary>One declaration of a class, with the semantic model of the file it lives in.</summary>
public sealed class ViewModelPart
{
    public ViewModelPart(ClassDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        Declaration = declaration;
        SemanticModel = semanticModel;
    }

    public ClassDeclarationSyntax Declaration { get; }

    /// <summary>Semantic models belong to a syntax tree, so each declaration carries its own.</summary>
    public SemanticModel SemanticModel { get; }
}

public class ViewModelAnalysis
{
    public string ClassName { get; set; } = "";
    public string Namespace { get; set; } = "";

    /// <summary>The declaration's type parameter list, such as <c>&lt;T&gt;</c>, or empty.</summary>
    public string TypeParameters { get; set; } = "";

    /// <summary>Enclosing types, outermost first, each with its own type parameters.</summary>
    public List<string> ContainingTypes { get; set; } = new List<string>();

    /// <summary>What the generator has to say about this class before generating anything.</summary>
    public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();

    /// <summary>
    /// Inherited property name to the members of this class that depend on it.
    /// </summary>
    /// <remarks>
    /// A base class raises its own property and knows nothing of what a subclass computed from it,
    /// so the subclass listens instead: these are forwarded from an OnPropertyChanged override.
    /// </remarks>
    public Dictionary<string, List<string>> InheritedDependencyNotifications { get; } =
        new Dictionary<string, List<string>>();

    /// <summary>Inherited property name to the commands of this class whose CanExecute reads it.</summary>
    public Dictionary<string, List<string>> InheritedCommandNotifications { get; } =
        new Dictionary<string, List<string>>();

    /// <summary>
    /// Namespace, enclosing types and name: what makes one view model distinguishable from another
    /// of the same name elsewhere in the project.
    /// </summary>
    public string FullyQualifiedName =>
        string.Join(".", new[] { Namespace }
            .Concat(ContainingTypes)
            .Concat(new[] { ClassName })
            .Where(part => !string.IsNullOrEmpty(part)));

    public List<BindingFieldInfo> BindingFields { get; set; } = new List<BindingFieldInfo>();
    public List<ComputedPropertyInfo> ComputedProperties { get; set; } = new List<ComputedPropertyInfo>();
    public List<CommandInfo> Commands { get; set; } = new List<CommandInfo>();
    public List<CanExecuteMethodInfo> CanExecuteMethods { get; set; } = new List<CanExecuteMethodInfo>();
    public List<PartialVoidMethodInfo> PartialVoidMethods { get; set; } = new List<PartialVoidMethodInfo>();

    public List<CollectionModifyingMethodInfo> CollectionModifyingMethods { get; set; } =
        new List<CollectionModifyingMethodInfo>();

    public Dictionary<string, List<string>> TransitiveDependencies { get; set; } =
        new Dictionary<string, List<string>>();

    public Dictionary<string, NotificationRequirements> NotificationRequirements { get; set; } =
        new Dictionary<string, NotificationRequirements>();
}

public class BindingFieldInfo
{
    /// <summary>
    /// Validation attributes written on the field, as source, to be re-emitted on the property.
    /// </summary>
    /// <remarks>
    /// An attribute on a field does not reach the property generated from it, and validation reads
    /// the property. Copying them across is what lets an application declare rules where it declares
    /// the field.
    /// </remarks>
    public List<string> ValidationAttributes { get; set; } = new List<string>();

    public string ClassName { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string FieldType { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public bool ReadOnly { get; set; }
}

public class ComputedPropertyInfo
{
    public string PropertyName { get; set; } = "";
    public ExpressionSyntax? Expression { get; set; }
    public List<string> DirectDependencies { get; set; } = new List<string>();
}

public class CommandInfo
{
    public string PropertyName { get; set; } = "";
    public List<string> CanExecuteReferences { get; set; } = new List<string>();
    public List<string> DirectDependencies { get; set; } = new List<string>();
}

public class CanExecuteMethodInfo
{
    public string MethodName { get; set; } = "";
    public List<string> DirectDependencies { get; set; } = new List<string>();
}

public class PartialVoidMethodInfo
{
    public string MethodName { get; set; } = "";
    public string PropertyName { get; set; } = "";
    public List<string> CalledMethods { get; set; } = new List<string>();

    /// <summary>
    /// Collections this hook modifies, whether in its own body or through anything it calls.
    /// </summary>
    public List<string> ModifiedCollections { get; set; } = new List<string>();

    /// <summary>Notifications raised by hand anywhere the hook reaches.</summary>
    public List<string> ManualNotifications { get; set; } = new List<string>();
}

public class CollectionModifyingMethodInfo
{
    public string MethodName { get; set; } = "";
    public List<string> ModifiedCollections { get; set; } = new List<string>();
    public List<string> ManualNotifications { get; set; } = new List<string>();
}

public class NotificationRequirements
{
    public string PropertyName { get; set; } = "";
    public List<string> ComputedPropertyNotifications { get; set; } = new List<string>();
    public List<string> CommandNotifications { get; set; } = new List<string>();
    public List<string> CollectionDependentNotifications { get; set; } = new List<string>();
}

#endregion
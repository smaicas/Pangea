using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    private ClassDeclarationSyntax _currentClass = null!;

    public ViewModelAnalysis AnalyzeViewModel(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        _currentClass = classDeclaration;

        ViewModelAnalysis analysis = new ViewModelAnalysis
        {
            ClassName = classDeclaration.Identifier.ValueText,
            Namespace = GetNamespace(classDeclaration),
            TypeParameters = classDeclaration.TypeParameterList?.ToString() ?? "",
            ContainingTypes = GetContainingTypes(classDeclaration)
        };

        // Phase 1: Inventory - Detectar todos los elementos funcionales
        InventoryBindingFields(classDeclaration, semanticModel, analysis);

        // Validation needs the inventory: two of the checks are about the names it produced.
        Validate(classDeclaration, semanticModel, analysis);
        InventoryComputedProperties(classDeclaration, analysis);
        InventoryCanExecuteElements(classDeclaration, analysis);
        InventoryCommands(classDeclaration, analysis);
        InventoryPartialVoidMethods(classDeclaration, analysis);
        InventoryCollectionModifyingMethods(classDeclaration, analysis);

        // Phase 2: Dependency Analysis - Construir grafo completo de dependencias
        BuildCompleteDependencyGraph(analysis);

        // Phase 3: Command Analysis - Analizar dependencias de comandos hacia binding fields
        AnalyzeCommandDependencies(analysis);

        // Phase 4: Generate notification requirements
        CalculateNotificationRequirements(analysis);

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
                PartialVoidMethodInfo partialMethodInfo = new PartialVoidMethodInfo
                {
                    MethodName = method.Identifier.ValueText,
                    CalledMethods = methodCalls,
                    PropertyName = ExtractPropertyNameFromOnChanged(method.Identifier.ValueText)
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
                MethodDeclarationSyntax? method = _currentClass.Members
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
            foreach (string calledMethod in partialMethod.CalledMethods)
            {
                CollectionModifyingMethodInfo? collectionMethod = analysis.CollectionModifyingMethods
                    .FirstOrDefault(cmm => cmm.MethodName == calledMethod);

                if (collectionMethod != null)
                {
                    result.AddRange(collectionMethod.ManualNotifications);

                    foreach (string modifiedCollection in collectionMethod.ModifiedCollections)
                    {
                        IEnumerable<string> dependentProperties = analysis.ComputedProperties
                            .Where(cp => cp.DirectDependencies.Contains(modifiedCollection))
                            .Select(cp => cp.PropertyName);

                        result.AddRange(dependentProperties);
                    }
                }
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

        return properties.Where(p => IsValidPropertyReference(p)).Distinct().ToList();
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
    private void Validate(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel,
        ViewModelAnalysis analysis)
    {
        if (analysis.BindingFields.Count == 0) return;

        Location classLocation = classDeclaration.Identifier.GetLocation();
        string className = classDeclaration.Identifier.ValueText;

        if (!classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            analysis.Diagnostics.Add(Diagnostic.Create(
                BindingDiagnostics.ClassMustBePartial, classLocation, className));
        }

        INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol != null && !DerivesFromViewModelBase(classSymbol))
        {
            analysis.Diagnostics.Add(Diagnostic.Create(
                BindingDiagnostics.ClassMustDeriveFromViewModelBase, classLocation, className));
        }

        ValidateStaticFields(classDeclaration, semanticModel, analysis);
        ValidateGeneratedNames(classDeclaration, classSymbol, analysis);
    }

    private static bool DerivesFromViewModelBase(INamedTypeSymbol classSymbol)
    {
        for (INamedTypeSymbol? current = classSymbol.BaseType; current != null; current = current.BaseType)
        {
            if (current.Name == "ViewModelBase") return true;
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
    private void ValidateGeneratedNames(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol? classSymbol,
        ViewModelAnalysis analysis)
    {
        HashSet<string> seen = new HashSet<string>();

        foreach (BindingFieldInfo field in analysis.BindingFields)
        {
            Location location = FindFieldLocation(classDeclaration, field.FieldName);

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
            }
        }
    }

    private static Location FindFieldLocation(ClassDeclarationSyntax classDeclaration, string fieldName)
    {
        VariableDeclaratorSyntax? declarator = classDeclaration.Members.OfType<FieldDeclarationSyntax>()
            .SelectMany(field => field.Declaration.Variables)
            .FirstOrDefault(variable => variable.Identifier.ValueText == fieldName);

        return declarator?.Identifier.GetLocation() ?? classDeclaration.Identifier.GetLocation();
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

        return new BindingFieldInfo
        {
            ClassName = fieldSymbol.ContainingType.Name,
            Namespace = fieldSymbol.ContainingType.ContainingNamespace.ToDisplayString(),
            FieldName = fieldSymbol.Name,
            FieldType = fieldSymbol.Type.ToDisplayString(),
            PropertyName = propertyName,
            ReadOnly = readOnly
        };
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
        return _currentClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName);
    }

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
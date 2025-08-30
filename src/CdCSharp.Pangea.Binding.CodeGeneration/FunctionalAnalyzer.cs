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
            ClassName = classDeclaration.Identifier.ValueText, Namespace = GetNamespace(classDeclaration)
        };

        // Phase 1: Inventory - Detectar todos los elementos funcionales
        InventoryBindingFields(classDeclaration, semanticModel, analysis);
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
        IEnumerable<PropertyDeclarationSyntax> commandPropertyDeclarations = classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => IsCommand(p));

        HashSet<string> commandProperties = new HashSet<string>(
            commandPropertyDeclarations.Select(p => p.Identifier.ValueText));

        // Buscar asignaciones de comandos en constructores
        IEnumerable<ConstructorDeclarationSyntax> constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>();

        foreach (ConstructorDeclarationSyntax constructor in constructors)
        {
            if (constructor.Body != null)
            {
                foreach (StatementSyntax statement in constructor.Body.Statements)
                {
                    AnalyzeConstructorStatement(statement, commandProperties, analysis);
                }
            }
        }
    }

    private void AnalyzeConstructorStatement(StatementSyntax statement, HashSet<string> commandProperties,
        ViewModelAnalysis analysis)
    {
        if (statement is ExpressionStatementSyntax exprStatement &&
            exprStatement.Expression is AssignmentExpressionSyntax assignment &&
            assignment.Left is IdentifierNameSyntax identifier &&
            commandProperties.Contains(identifier.Identifier.ValueText))
        {
            string commandName = identifier.Identifier.ValueText;
            CommandInfo commandInfo = new CommandInfo
            {
                PropertyName = commandName,
                CanExecuteReferences = new List<string>(),
                DirectDependencies = new List<string>()
            };

            // Analizar el lado derecho de la asignación
            AnalyzeCommandAssignment(assignment.Right, commandInfo, analysis);

            analysis.Commands.Add(commandInfo);
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
            if (method.Body != null)
            {
                List<string> collectionModifications = DetectCollectionModifications(method.Body);
                if (collectionModifications.Any())
                {
                    CollectionModifyingMethodInfo methodInfo = new CollectionModifyingMethodInfo
                    {
                        MethodName = method.Identifier.ValueText,
                        ModifiedCollections = collectionModifications,
                        ManualNotifications = ExtractManualNotifications(method.Body)
                    };

                    analysis.CollectionModifyingMethods.Add(methodInfo);
                }
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

            foreach (string canExecuteRef in command.CanExecuteReferences)
            {
                // Encontrar todas las binding properties de las que depende este CanExecute
                HashSet<string> dependencies = GetAllBindingDependencies(canExecuteRef, analysis);
                bindingDependencies.UnionWith(dependencies);
            }

            // Si no hay CanExecute explícito, analizar el método Execute para inferir dependencias implícitas
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
        HashSet<string> result = new HashSet<string>();

        // Si el elemento es directamente una binding property
        if (analysis.BindingFields.Any(b => b.PropertyName == element))
        {
            result.Add(element);
            return result;
        }

        // Si el elemento tiene dependencias transitivas calculadas
        if (analysis.TransitiveDependencies.TryGetValue(element, out List<string>? transitiveDeps))
        {
            foreach (string dep in transitiveDeps)
            {
                if (analysis.BindingFields.Any(b => b.PropertyName == dep))
                {
                    result.Add(dep);
                }
            }
        }

        // Buscar dependencias directas si no se encontraron transitivas
        if (!result.Any())
        {
            // Buscar en computed properties
            ComputedPropertyInfo? computedProp = analysis.ComputedProperties
                .FirstOrDefault(cp => cp.PropertyName == element);

            if (computedProp != null)
            {
                foreach (string directDep in computedProp.DirectDependencies)
                {
                    result.UnionWith(GetAllBindingDependencies(directDep, analysis));
                }
            }

            // Buscar en CanExecute methods
            CanExecuteMethodInfo? canExecuteMethod = analysis.CanExecuteMethods
                .FirstOrDefault(cem => cem.MethodName == element);

            if (canExecuteMethod != null)
            {
                foreach (string directDep in canExecuteMethod.DirectDependencies)
                {
                    result.UnionWith(GetAllBindingDependencies(directDep, analysis));
                }
            }
        }

        return result;
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

            // Si el comando depende directamente de esta propiedad
            if (command.DirectDependencies.Contains(propertyName))
            {
                shouldNotify = true;
            }
            else
            {
                // Si alguna de las CanExecute referencias depende de esta propiedad
                foreach (string canExecuteRef in command.CanExecuteReferences)
                {
                    if (analysis.TransitiveDependencies.TryGetValue(canExecuteRef, out List<string>? deps) &&
                        deps.Contains(propertyName))
                    {
                        shouldNotify = true;
                        break;
                    }

                    // Verificar dependencias directas también
                    CanExecuteMethodInfo? canExecuteMethod = analysis.CanExecuteMethods
                        .FirstOrDefault(cem => cem.MethodName == canExecuteRef);

                    if (canExecuteMethod != null && canExecuteMethod.DirectDependencies.Contains(propertyName))
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
                    // Solo ExecuteMethod - analizar para dependencias implícitas
                    AnalyzeExecuteMethodForImplicitDependencies(commandInfo, analysis, new HashSet<string>());
                }
            }
        }
    }

    private void AnalyzeCanExecuteArgument(ExpressionSyntax canExecuteArg, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        switch (canExecuteArg)
        {
            // Caso: () => CanSave
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

            // Caso: Referencia directa a método - CanExecuteComplexOperation
            case IdentifierNameSyntax identifier:
                commandInfo.CanExecuteReferences.Add(identifier.Identifier.ValueText);
                break;
        }
    }

    private void AnalyzeLambdaExpression(ExpressionSyntax expression, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        switch (expression)
        {
            // Caso simple: CanSave
            case IdentifierNameSyntax identifier:
                commandInfo.CanExecuteReferences.Add(identifier.Identifier.ValueText);
                break;

            // Caso complejo: CanRecord || CanStopRecording, !IsLoading && IsEnabled
            case BinaryExpressionSyntax binaryExpression:
                AnalyzeBinaryExpression(binaryExpression, commandInfo, analysis);
                break;

            // Caso: !IsLoading
            case PrefixUnaryExpressionSyntax prefixUnary:
                AnalyzePrefixUnaryExpression(prefixUnary, commandInfo, analysis);
                break;

            // Caso: string.IsNullOrEmpty(item) - llamada a método
            case InvocationExpressionSyntax invocation:
                AnalyzeInvocationInLambda(invocation, commandInfo, analysis);
                break;

            // Caso: Items.Count, item.Length
            case MemberAccessExpressionSyntax memberAccess:
                AnalyzeMemberAccessInLambda(memberAccess, commandInfo, analysis);
                break;
        }
    }

    private void AnalyzePrefixUnaryExpression(PrefixUnaryExpressionSyntax prefixUnary, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Para casos como !IsLoading, !HasErrors
        if (prefixUnary.Operand is IdentifierNameSyntax identifier)
        {
            commandInfo.CanExecuteReferences.Add(identifier.Identifier.ValueText);
        }
        else if (prefixUnary.Operand is MemberAccessExpressionSyntax memberAccess)
        {
            AnalyzeMemberAccessInLambda(memberAccess, commandInfo, analysis);
        }
        else
        {
            // Analizar recursivamente operandos complejos
            AnalyzeLambdaExpression(prefixUnary.Operand, commandInfo, analysis);
        }
    }

    private void AnalyzeInvocationInLambda(InvocationExpressionSyntax invocation, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Para casos como string.IsNullOrEmpty(item), Email.Contains("@")
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is IdentifierNameSyntax argIdentifier)
            {
                // Solo agregar si es una propiedad conocida del ViewModel
                if (analysis.BindingFields.Any(bf => bf.PropertyName == argIdentifier.Identifier.ValueText) ||
                    analysis.ComputedProperties.Any(cp => cp.PropertyName == argIdentifier.Identifier.ValueText))
                {
                    commandInfo.CanExecuteReferences.Add(argIdentifier.Identifier.ValueText);
                }
            }
        }

        // También analizar la expresión de la invocación (el método que se llama)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            AnalyzeMemberAccessInLambda(memberAccess, commandInfo, analysis);
        }
    }


    private void AnalyzeMemberAccessInLambda(MemberAccessExpressionSyntax memberAccess, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Para casos como Items.Count, Email.Length
        if (memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            string propertyName = identifier.Identifier.ValueText;

            // Verificar si es una propiedad conocida del ViewModel
            if (analysis.BindingFields.Any(bf => bf.PropertyName == propertyName) ||
                analysis.ComputedProperties.Any(cp => cp.PropertyName == propertyName))
            {
                commandInfo.CanExecuteReferences.Add(propertyName);
            }
        }
    }

    private void AnalyzeBinaryExpression(BinaryExpressionSyntax binaryExpression, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Analizar recursivamente ambos lados de la expresión binaria
        AnalyzeLambdaExpression(binaryExpression.Left, commandInfo, analysis);
        AnalyzeLambdaExpression(binaryExpression.Right, commandInfo, analysis);
    }


    private void AnalyzeMemberAccess(MemberAccessExpressionSyntax memberAccess, CommandInfo commandInfo)
    {
        if (memberAccess.Expression is IdentifierNameSyntax identifier)
        {
            // Items.Count -> agregar "Items" como dependencia
            commandInfo.CanExecuteReferences.Add(identifier.Identifier.ValueText);
        }
    }

    private void AnalyzeLambdaBlock(BlockSyntax block, CommandInfo commandInfo, ViewModelAnalysis analysis)
    {
        foreach (StatementSyntax statement in block.Statements)
        {
            if (statement is ReturnStatementSyntax returnStatement &&
                returnStatement.Expression != null)
            {
                AnalyzeLambdaExpression(returnStatement.Expression, commandInfo, analysis);
            }
            else if (statement is ExpressionStatementSyntax exprStatement)
            {
                AnalyzeLambdaExpression(exprStatement.Expression, commandInfo, analysis);
            }
        }
    }

    private void AnalyzeInvocationExpression(InvocationExpressionSyntax invocation, CommandInfo commandInfo,
        ViewModelAnalysis analysis)
    {
        // Para casos como !string.IsNullOrEmpty(item), capturar referencias a propiedades del ViewModel
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is IdentifierNameSyntax identifier)
            {
                // Solo agregar si es una propiedad conocida del ViewModel, no un parámetro
                if (analysis.BindingFields.Any(bf => bf.PropertyName == identifier.Identifier.ValueText) ||
                    analysis.ComputedProperties.Any(cp => cp.PropertyName == identifier.Identifier.ValueText))
                {
                    commandInfo.CanExecuteReferences.Add(identifier.Identifier.ValueText);
                }
            }
        }
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
        return property.Type.ToString().Contains("RelayCommand");
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

        foreach (SyntaxNode node in body.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                string? methodName = ExtractMethodName(invocation.Expression);
                if (!string.IsNullOrEmpty(methodName))
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

    private List<string> DetectCollectionModifications(BlockSyntax methodBody)
    {
        List<string> modifications = new List<string>();

        foreach (SyntaxNode node in methodBody.DescendantNodes())
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

    private List<string> ExtractManualNotifications(BlockSyntax methodBody)
    {
        List<string> notifications = new List<string>();

        foreach (SyntaxNode node in methodBody.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "OnPropertyChanged")
            {
                ArgumentSyntax? arg = invocation.ArgumentList.Arguments.FirstOrDefault();
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
            }
        }

        return notifications;
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

    public List<BindingFieldInfo> BindingFields { get; set; } = new List<BindingFieldInfo>();
    public List<ComputedPropertyInfo> ComputedProperties { get; set; } = new List<ComputedPropertyInfo>();
    public List<CommandInfo> Commands { get; set; } = new List<CommandInfo>();
    public List<CanExecuteMethodInfo> CanExecuteMethods { get; set; } = new List<CanExecuteMethodInfo>();
    public List<PartialVoidMethodInfo> PartialVoidMethods { get; set; } = new List<PartialVoidMethodInfo>();

    public List<CollectionModifyingMethodInfo> CollectionModifyingMethods { get; set; } =
        new List<CollectionModifyingMethodInfo>();

    public Dictionary<string, List<string>> DependencyGraph { get; set; } = new Dictionary<string, List<string>>();

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
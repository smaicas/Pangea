using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace CdCSharp.Pangea.Binding.CodeGeneration;

/// <summary>
/// Analizador funcional profesional que detecta todas las dependencias entre propiedades,
/// computed properties, métodos CanExecute y comandos para generar notificaciones automáticas
/// </summary>
public class FunctionalAnalyzer
{
    public ViewModelAnalysis AnalyzeViewModel(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        ViewModelAnalysis analysis = new ViewModelAnalysis
        {
            ClassName = classDeclaration.Identifier.ValueText,
            Namespace = GetNamespace(classDeclaration)
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

    private void InventoryBindingFields(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel, ViewModelAnalysis analysis)
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
                MethodName = method.Identifier.ValueText,
                DirectDependencies = dependencies
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
                MethodName = property.Identifier.ValueText,
                DirectDependencies = dependencies
            };

            analysis.CanExecuteMethods.Add(methodInfo);
        }
    }

    private void InventoryCommands(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
    {
        // Obtener todas las propiedades de comando
        IEnumerable<string> commandPropertyNames = classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => IsCommand(p))
            .Select(p => p.Identifier.ValueText);
        
        HashSet<string> commandProperties = new HashSet<string>(commandPropertyNames);

        // Buscar asignaciones de comandos en constructores
        IEnumerable<ConstructorDeclarationSyntax> constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>();

        foreach (ConstructorDeclarationSyntax constructor in constructors)
        {
            if (constructor.Body != null)
            {
                foreach (StatementSyntax statement in constructor.Body.Statements)
                {
                    if (statement is ExpressionStatementSyntax expressionStatement &&
                        expressionStatement.Expression is AssignmentExpressionSyntax assignment)
                    {
                        AnalyzeCommandAssignment(assignment, analysis, commandProperties);
                    }
                }
            }
        }

        // Comandos como propiedades con ExpressionBody (como ToggleRecordingCommand)
        IEnumerable<PropertyDeclarationSyntax> commandPropertiesDecl = classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => IsCommand(p) && p.ExpressionBody?.Expression is InvocationExpressionSyntax);

        foreach (PropertyDeclarationSyntax property in commandPropertiesDecl)
        {
            if (property.ExpressionBody?.Expression is InvocationExpressionSyntax invocation)
            {
                List<string> canExecuteRefs = ExtractCanExecuteReferences(invocation);
                
                CommandInfo commandInfo = new CommandInfo
                {
                    PropertyName = property.Identifier.ValueText,
                    CanExecuteReferences = canExecuteRefs
                };

                analysis.Commands.Add(commandInfo);
            }
        }
    }

    private void AnalyzeCommandAssignment(AssignmentExpressionSyntax assignment, ViewModelAnalysis analysis, HashSet<string> commandProperties)
    {
        if (assignment.Left is IdentifierNameSyntax commandProperty &&
            commandProperties.Contains(commandProperty.Identifier.ValueText) &&
            assignment.Right is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax methodName &&
            methodName.Identifier.ValueText == "CreateCommand")
        {
            List<string> canExecuteRefs = ExtractCanExecuteReferences(invocation);
            
            CommandInfo commandInfo = new CommandInfo
            {
                PropertyName = commandProperty.Identifier.ValueText,
                CanExecuteReferences = canExecuteRefs
            };

            // Analisis específico para lambdas que referencian propiedades CanExecute
            AnalyzeLambdaCanExecuteReferences(invocation, commandInfo, analysis);

            analysis.Commands.Add(commandInfo);
        }
    }

    private void AnalyzeLambdaCanExecuteReferences(InvocationExpressionSyntax invocation, CommandInfo commandInfo, ViewModelAnalysis analysis)
    {
        ArgumentSyntax[] arguments = invocation.ArgumentList.Arguments.ToArray();
        
        // Buscar específicamente el segundo argumento (CanExecute)
        if (arguments.Length > 1)
        {
            ArgumentSyntax canExecuteArg = arguments[1];
            
            if (canExecuteArg.Expression is SimpleLambdaExpressionSyntax lambda &&
                lambda.Body is IdentifierNameSyntax identifier)
            {
                string identifierName = identifier.Identifier.ValueText;
                
                // Si es una propiedad CanExecute, agregarla
                if (identifierName.StartsWith("Can"))
                {
                    if (!commandInfo.CanExecuteReferences.Contains(identifierName))
                    {
                        commandInfo.CanExecuteReferences.Add(identifierName);
                    }
                }
                // Si no es un CanExecute explícito pero parece ser una propiedad de estado,
                // intentar mapear por convención
                else
                {
                    string commandName = commandInfo.PropertyName;
                    if (commandName.EndsWith("Command"))
                    {
                        string baseName = commandName.Substring(0, commandName.Length - 7);
                        string expectedCanExecute = $"Can{baseName}";
                        
                        // Verificar si existe la propiedad CanExecute correspondiente
                        bool hasCorrespondingCanExecute = analysis.CanExecuteMethods
                            .Any(cem => cem.MethodName == expectedCanExecute);
                            
                        if (hasCorrespondingCanExecute)
                        {
                            commandInfo.CanExecuteReferences.Add(expectedCanExecute);
                        }
                    }
                }
            }
        }
    }

    private List<string> ExtractCanExecuteReferences(InvocationExpressionSyntax invocation)
    {
        List<string> references = new List<string>();

        // Analizar argumentos del CreateCommand buscando CanExecute
        // El CanExecute siempre es el SEGUNDO argumento (después del Execute)
        ArgumentSyntax[] arguments = invocation.ArgumentList.Arguments.ToArray();
        
        for (int i = 1; i < arguments.Length; i++) // Empezar desde índice 1 (segundo argumento)
        {
            ArgumentSyntax argument = arguments[i];
            
            if (argument.Expression is SimpleLambdaExpressionSyntax lambda)
            {
                // Extraer TODAS las referencias de la lambda, sin filtrar por "Can"
                List<string> lambdaReferences = ExtractAllReferencesFromLambda(lambda);
                references.AddRange(lambdaReferences);
            }
            else if (argument.Expression is IdentifierNameSyntax directIdentifier)
            {
                // Caso: CreateCommand(Execute, CanExecuteMethod)
                string identifierName = directIdentifier.Identifier.ValueText;
                if (identifierName.StartsWith("Can"))
                {
                    references.Add(identifierName);
                }
            }
        }

        return references.Distinct().ToList();
    }

    private List<string> ExtractAllReferencesFromLambda(SimpleLambdaExpressionSyntax lambda)
    {
        List<string> references = new List<string>();
        
        if (lambda.Body is ExpressionSyntax lambdaBody)
        {
            // Usar el método mejorado para extraer todas las referencias
            List<string> allIdentifiers = ExtractIdentifiersFromExpression(lambdaBody);
            
            // Filtrar para incluir solo referencias relevantes de CanExecute
            foreach (string identifier in allIdentifiers)
            {
                if (IsCanExecuteReference(identifier))
                {
                    references.Add(identifier);
                }
            }
        }
        
        return references;
    }

    private bool IsCanExecuteReference(string identifier)
    {
        // Identificar referencias típicas de CanExecute
        return identifier.StartsWith("Can") ||          // CanSave, CanSubmit, etc.
               identifier.StartsWith("Is") ||           // IsLoading, IsEnabled, etc.
               identifier.StartsWith("Has") ||          // HasErrors, HasItems, etc.
               identifier == "Age" ||                   // Propiedades específicas usadas en CanExecute
               identifier == "ItemCount" ||
               identifier == "Email" ||
               identifier.Contains("Loading") ||
               identifier.Contains("Error") ||
               identifier.Contains("Online") ||
               identifier.Contains("Authenticated") ||
               identifier.Contains("Enabled") ||
               identifier.Contains("Recording");
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

    private void InventoryCollectionModifyingMethods(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
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

        // Resolver dependencias transitivas usando algoritmo de cierre transitivo
        foreach (string property in directDependencies.Keys.ToList())
        {
            HashSet<string> allDependencies = ComputeTransitiveDependencies(property, directDependencies, new HashSet<string>());
            analysis.TransitiveDependencies[property] = allDependencies.ToList();
        }
    }

    private HashSet<string> ComputeTransitiveDependencies(string property, Dictionary<string, HashSet<string>> directDependencies, HashSet<string> visited)
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

            // Si no se detectaron CanExecute explícitos, intentar mapear por convención de nombres
            if (!command.CanExecuteReferences.Any() || !bindingDependencies.Any())
            {
                TryMapCommandByConvention(command, analysis, bindingDependencies);
            }

            command.DirectDependencies = bindingDependencies.ToList();
        }
    }

    private void TryMapCommandByConvention(CommandInfo command, ViewModelAnalysis analysis, HashSet<string> bindingDependencies)
    {
        // Intentar mapear comandos por convención de nombres
        // SaveCommand -> CanSave, SubmitCommand -> CanSubmit, etc.
        
        string commandName = command.PropertyName;
        if (commandName.EndsWith("Command"))
        {
            string baseName = commandName.Substring(0, commandName.Length - 7); // Remover "Command"
            string canExecuteName = $"Can{baseName}";
            
            // Buscar CanExecute method/property correspondiente
            CanExecuteMethodInfo? canExecuteMethod = analysis.CanExecuteMethods
                .FirstOrDefault(cem => cem.MethodName == canExecuteName);
                
            if (canExecuteMethod != null)
            {
                command.CanExecuteReferences.Add(canExecuteName);
                HashSet<string> dependencies = GetAllBindingDependencies(canExecuteName, analysis);
                bindingDependencies.UnionWith(dependencies);
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
        foreach (BindingFieldInfo binding in analysis.BindingFields)
        {
            NotificationRequirements requirements = new NotificationRequirements
            {
                PropertyName = binding.PropertyName
            };

            // Computed properties que dependen de esta binding property
            requirements.ComputedPropertyNotifications = analysis.ComputedProperties
                .Where(cp => DependsOnBinding(cp.PropertyName, binding.PropertyName, analysis))
                .Select(cp => cp.PropertyName)
                .ToList();

            // Comandos que dependen de esta binding property
            requirements.CommandNotifications = analysis.Commands
                .Where(cmd => cmd.DirectDependencies.Contains(binding.PropertyName))
                .Select(cmd => cmd.PropertyName)
                .ToList();

            // Collection-dependent notifications
            requirements.CollectionDependentNotifications = GetCollectionDependentProperties(binding.PropertyName, analysis);

            analysis.NotificationRequirements[binding.PropertyName] = requirements;
        }
    }

    private bool DependsOnBinding(string element, string bindingProperty, ViewModelAnalysis analysis)
    {
        if (analysis.TransitiveDependencies.TryGetValue(element, out List<string>? deps))
        {
            return deps.Contains(bindingProperty);
        }

        // Fallback: buscar dependencias directas
        ComputedPropertyInfo? computedProp = analysis.ComputedProperties
            .FirstOrDefault(cp => cp.PropertyName == element);
        
        return computedProp?.DirectDependencies.Contains(bindingProperty) == true;
    }

    private List<string> GetCollectionDependentProperties(string propertyName, ViewModelAnalysis analysis)
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

        // Usar DescendantNodes para obtener TODOS los nodos, incluyendo dentro de expresiones binarias
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

        // Usar DescendantNodes para obtener TODOS los nodos
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

    // Otros métodos helper...
    private List<string> ExtractMethodCalls(MethodDeclarationSyntax method)
    {
        List<string> methodCalls = new List<string>();
        SyntaxNode? body = method.Body ?? (SyntaxNode?)method.ExpressionBody?.Expression;
        if (body == null) return methodCalls;

        foreach (SyntaxNode node in body.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation)
            {
                string? methodName = null;
                    
                if (invocation.Expression is IdentifierNameSyntax identifier)
                {
                    methodName = identifier.Identifier.ValueText;
                }
                else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                         memberAccess.Expression is ThisExpressionSyntax)
                {
                    methodName = memberAccess.Name.Identifier.ValueText;
                }

                if (methodName != null)
                {
                    methodCalls.Add(methodName);
                }
            }
        }

        return methodCalls;
    }

    private string ExtractPropertyNameFromOnChanged(string methodName)
    {
        if (methodName.StartsWith("On") && methodName.EndsWith("Changed"))
        {
            return methodName.Substring(2, methodName.Length - 9);
        }
        return "";
    }

    private List<string> DetectCollectionModifications(BlockSyntax methodBody)
    {
        List<string> modifiedCollections = new List<string>();

        foreach (SyntaxNode node in methodBody.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                string methodName = memberAccess.Name.Identifier.ValueText;
                    
                if (methodName is "Add" or "Remove" or "Clear" or "Insert" or "RemoveAt")
                {
                    if (memberAccess.Expression is IdentifierNameSyntax collection)
                    {
                        modifiedCollections.Add(collection.Identifier.ValueText);
                    }
                }
            }
        }

        return modifiedCollections.Distinct().ToList();
    }

    private List<string> ExtractManualNotifications(BlockSyntax methodBody)
    {
        List<string> notifications = new List<string>();

        foreach (SyntaxNode node in methodBody.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax invocation &&
                invocation.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == "OnPropertyChanged")
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
        return baseName.Length > 0 ? 
            char.ToUpperInvariant(baseName[0]) + baseName.Substring(1) : baseName;
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
    public List<CollectionModifyingMethodInfo> CollectionModifyingMethods { get; set; } = new List<CollectionModifyingMethodInfo>();
        
    public Dictionary<string, List<string>> DependencyGraph { get; set; } = new Dictionary<string, List<string>>();
    public Dictionary<string, List<string>> TransitiveDependencies { get; set; } = new Dictionary<string, List<string>>();
    public Dictionary<string, NotificationRequirements> NotificationRequirements { get; set; } = new Dictionary<string, NotificationRequirements>();
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
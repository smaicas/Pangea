using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace CdCSharp.Pangea.Binding.CodeGeneration;

public class FunctionalAnalyzer
{
    public ViewModelAnalysis AnalyzeViewModel(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        ViewModelAnalysis analysis = new ViewModelAnalysis
        {
            ClassName = classDeclaration.Identifier.ValueText,
            Namespace = GetNamespace(classDeclaration)
        };

        // Phase 1: Inventory - Detect all functional elements
        InventoryBindingFields(classDeclaration, semanticModel, analysis);
        InventoryComputedProperties(classDeclaration, analysis);
        InventoryCommands(classDeclaration, analysis);
        InventoryCanExecuteMethods(classDeclaration, analysis);
        InventoryPartialVoidMethods(classDeclaration, analysis);
        InventoryCollectionModifyingMethods(classDeclaration, analysis);

        // Phase 2: Dependency Analysis - Build dependency graph
        AnalyzeDependencies(analysis);

        // Phase 3: Generate notification requirements
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
            .Where(p => p.ExpressionBody != null && !IsCommand(p));

        foreach (PropertyDeclarationSyntax property in computedProperties)
        {
            ComputedPropertyInfo computedInfo = new ComputedPropertyInfo
            {
                PropertyName = property.Identifier.ValueText,
                Expression = property.ExpressionBody.Expression,
                DirectDependencies = ExtractPropertiesFromExpression(property.ExpressionBody.Expression)
            };

            analysis.ComputedProperties.Add(computedInfo);
        }
    }

    private void InventoryCommands(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
    {
        IEnumerable<PropertyDeclarationSyntax> commandProperties = classDeclaration.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => IsCommand(p) && p.ExpressionBody?.Expression is InvocationExpressionSyntax);

        foreach (PropertyDeclarationSyntax property in commandProperties)
        {
            if (property.ExpressionBody?.Expression is InvocationExpressionSyntax invocation)
            {
                CommandInfo commandInfo = new CommandInfo
                {
                    PropertyName = property.Identifier.ValueText,
                    DirectDependencies = ExtractCommandDependencies(invocation)
                };

                analysis.Commands.Add(commandInfo);
            }
        }
    }

    private void InventoryCanExecuteMethods(ClassDeclarationSyntax classDeclaration, ViewModelAnalysis analysis)
    {
        IEnumerable<MethodDeclarationSyntax> canExecuteMethods = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText.StartsWith("Can") && 
                        m.ReturnType.ToString() == "bool");

        foreach (MethodDeclarationSyntax method in canExecuteMethods)
        {
            if (method.Body != null)
            {
                CanExecuteMethodInfo methodInfo = new CanExecuteMethodInfo
                {
                    MethodName = method.Identifier.ValueText,
                    DirectDependencies = ExtractPropertiesFromStatement(method.Body)
                };

                analysis.CanExecuteMethods.Add(methodInfo);
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

    #region Phase 2: Dependency Analysis

    private void AnalyzeDependencies(ViewModelAnalysis analysis)
    {
        foreach (BindingFieldInfo binding in analysis.BindingFields)
        {
            List<string> allDependents = new List<string>();

            IEnumerable<string> directComputedDependents = analysis.ComputedProperties
                .Where(cp => cp.DirectDependencies.Contains(binding.PropertyName))
                .Select(cp => cp.PropertyName);
            allDependents.AddRange(directComputedDependents);

            List<string> transitiveDependents = GetTransitiveDependents(binding.PropertyName, analysis);
            allDependents.AddRange(transitiveDependents);

            IEnumerable<string> commandDependents = analysis.Commands
                .Where(cmd => DependsOn(cmd, binding.PropertyName, analysis))
                .Select(cmd => cmd.PropertyName);
            allDependents.AddRange(commandDependents);

            List<string> collectionDependents = GetCollectionDependentProperties(binding.PropertyName, analysis);
            allDependents.AddRange(collectionDependents);

            analysis.DependencyGraph[binding.PropertyName] = allDependents.Distinct().ToList();
        }
    }

    private List<string> GetTransitiveDependents(string propertyName, ViewModelAnalysis analysis)
    {
        List<string> result = new List<string>();
        HashSet<string> visited = new HashSet<string>();

        void FindTransitiveDependents(string prop)
        {
            if (visited.Contains(prop)) return;
            visited.Add(prop);

            IEnumerable<string> dependents = analysis.ComputedProperties
                .Where(cp => cp.DirectDependencies.Contains(prop))
                .Select(cp => cp.PropertyName);

            foreach (string dependent in dependents)
            {
                result.Add(dependent);
                FindTransitiveDependents(dependent);
            }
        }

        FindTransitiveDependents(propertyName);
        return result;
    }

    private bool DependsOn(CommandInfo command, string propertyName, ViewModelAnalysis analysis)
    {
        if (command.DirectDependencies.Contains(propertyName))
            return true;

        foreach (string dependency in command.DirectDependencies)
        {
            ComputedPropertyInfo? computedProp = analysis.ComputedProperties
                .FirstOrDefault(cp => cp.PropertyName == dependency);

            if (computedProp != null && 
                (computedProp.DirectDependencies.Contains(propertyName) ||
                 GetTransitiveDependents(propertyName, analysis).Contains(dependency)))
            {
                return true;
            }
        }

        return false;
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

    #region Phase 3: Notification Requirements

    private void CalculateNotificationRequirements(ViewModelAnalysis analysis)
    {
        foreach (BindingFieldInfo binding in analysis.BindingFields)
        {
            NotificationRequirements requirements = new NotificationRequirements
            {
                PropertyName = binding.PropertyName
            };

            if (analysis.DependencyGraph.TryGetValue(binding.PropertyName, out List<string>? dependents))
            {
                requirements.ComputedPropertyNotifications = dependents
                    .Where(d => analysis.ComputedProperties.Any(cp => cp.PropertyName == d))
                    .ToList();

                requirements.CommandNotifications = dependents
                    .Where(d => analysis.Commands.Any(cmd => cmd.PropertyName == d))
                    .ToList();

                requirements.CollectionDependentNotifications = GetCollectionDependentProperties(binding.PropertyName, analysis);
            }

            analysis.NotificationRequirements[binding.PropertyName] = requirements;
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

    private List<string> ExtractPropertiesFromExpression(ExpressionSyntax expression)
    {
        List<string> properties = new List<string>();

        foreach (SyntaxNode node in expression.DescendantNodes())
        {
            if (node is IdentifierNameSyntax identifier)
            {
                string name = identifier.Identifier.ValueText;
                if (char.IsUpper(name[0]) && !IsSystemType(name))
                {
                    properties.Add(name);
                }
            }
        }

        return properties.Distinct().ToList();
    }

    private List<string> ExtractPropertiesFromStatement(BlockSyntax block)
    {
        List<string> properties = new List<string>();

        foreach (SyntaxNode node in block.DescendantNodes())
        {
            if (node is IdentifierNameSyntax identifier)
            {
                string name = identifier.Identifier.ValueText;
                if (char.IsUpper(name[0]) && !IsSystemType(name))
                {
                    properties.Add(name);
                }
            }
        }

        return properties.Distinct().ToList();
    }

    private List<string> ExtractCommandDependencies(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count < 2) return new List<string>();

        ExpressionSyntax canExecuteArg = invocation.ArgumentList.Arguments[1].Expression;
            
        return canExecuteArg switch
        {
            LambdaExpressionSyntax lambda => ExtractPropertiesFromLambda(lambda),
            IdentifierNameSyntax identifier => new List<string> { identifier.Identifier.ValueText },
            _ => new List<string>()
        };
    }

    private List<string> ExtractPropertiesFromLambda(LambdaExpressionSyntax lambda)
    {
        return lambda.Body switch
        {
            ExpressionSyntax expression => ExtractPropertiesFromExpression(expression),
            BlockSyntax block => ExtractPropertiesFromStatement(block),
            _ => new List<string>()
        };
    }

    private string GetNamespace(SyntaxNode node)
    {
        while (node != null)
        {
            if (node is NamespaceDeclarationSyntax namespaceDecl)
                return namespaceDecl.Name.ToString();
            if (node is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                return fileScopedNamespace.Name.ToString();
            node = node.Parent;
        }
        return "";
    }

    private (string? PropertyName, bool ReadOnly) GetBindingConfiguration(AttributeData? bindingAttribute)
    {
        if (bindingAttribute == null) return (null, false);

        string? customPropertyName = null;
        bool readOnly = false;

        foreach (KeyValuePair<string, TypedConstant> namedArg in bindingAttribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "PropertyName" when namedArg.Value.Value != null:
                    customPropertyName = namedArg.Value.Value.ToString();
                    break;
                case "ReadOnly" when namedArg.Value.Value != null:
                    readOnly = (bool)namedArg.Value.Value;
                    break;
            }

        return (customPropertyName, readOnly);
    }

    private string GeneratePropertyName(string fieldName)
    {
        if (fieldName.StartsWith("_"))
            fieldName = fieldName.Substring(1);

        return fieldName.Length > 0 ? char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1) : fieldName;
    }

    private bool IsSystemType(string name)
    {
        return name is "String" or "Int32" or "Boolean" or "Object" or "DateTime";
    }

    #endregion
}

#region Analysis Data Structures

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
using System.Collections.Concurrent;
using System.Reflection;

namespace CdCSharp.Pangea.Core.Base;

public class TypeRegistry
{
    private static readonly Lazy<TypeRegistry> _instance = new(() => new TypeRegistry());
    public static TypeRegistry Instance => _instance.Value;
    
    private readonly ConcurrentDictionary<string, Type> _typeByName = new();
    private readonly ConcurrentDictionary<Type, List<Type>> _typesByBaseType = new();
    private readonly ConcurrentDictionary<Type, List<Type>> _typesByInterface = new();
    private readonly ConcurrentDictionary<Type, List<Type>> _typesByAttribute = new();
    private readonly ConcurrentDictionary<Type, List<(Type Type, object Attribute)>> _typesWithAttributeData = new();
    private readonly ConcurrentDictionary<Assembly, List<(Type AttributeType, object Attribute, Type? FeatureType)>> _assemblyAttributes = new();
    private readonly HashSet<Assembly> _loadedAssemblies = new();
    private readonly object _lock = new object();
    private bool _isInitialized = false;
    
    private TypeRegistry() { }
    public void Initialize()
    {
        if (_isInitialized) return;
        lock (_lock)
        {
            if (_isInitialized) return;
            LoadAllAssemblies();
            CacheAllTypes();
            _isInitialized = true;
        }
    }

    public async Task InitializeAsync()
    {
        await Task.Run(() => Initialize());
    }

    private void LoadAllAssemblies()
    {
        HashSet<Assembly> assemblies = new HashSet<Assembly>();
        try
        {
            if (Assembly.GetExecutingAssembly() != null)
                assemblies.Add(Assembly.GetExecutingAssembly());
            if (Assembly.GetEntryAssembly() != null)
                assemblies.Add(Assembly.GetEntryAssembly());
            if (Assembly.GetCallingAssembly() != null)
                assemblies.Add(Assembly.GetCallingAssembly());
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!IsSystemOrAvaloniaAssembly(assembly))
                    assemblies.Add(assembly);
            }
            HashSet<string?> loadedNames = assemblies.Select(a => a.GetName().Name).ToHashSet();
            Queue<Assembly> toLoad = new Queue<Assembly>(assemblies);
            while (toLoad.Count > 0)
            {
                Assembly current = toLoad.Dequeue();
                try
                {
                    foreach (AssemblyName referencedAssembly in current.GetReferencedAssemblies())
                    {
                        if (!loadedNames.Contains(referencedAssembly.Name) && 
                            !IsSystemOrAvaloniaAssembly(referencedAssembly.Name))
                        {
                            try
                            {
                                Assembly loaded = Assembly.Load(referencedAssembly);
                                if (assemblies.Add(loaded))
                                {
                                    loadedNames.Add(referencedAssembly.Name);
                                    toLoad.Enqueue(loaded);
                                }
                            }
                            catch
                            {
                                // Ignorar ensamblados que no se pueden cargar
                            }
                        }
                    }
                }
                catch
                {
                    // Ignorar errores al obtener referencias
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando ensamblados: {ex.Message}");
        }
        _loadedAssemblies.UnionWith(assemblies);
    }

    private bool IsSystemOrAvaloniaAssembly(Assembly assembly)
    {
        return IsSystemOrAvaloniaAssembly(assembly.GetName().Name);
    }

    private bool IsSystemOrAvaloniaAssembly(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName)) return true;
        string[] systemPrefixes = new[]
        {
            "System.",
            "Microsoft.",
            "mscorlib",
            "netstandard",
            "Avalonia.",
            "WindowsBase",
            "PresentationCore",
            "PresentationFramework"
        };
        return systemPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private void CacheAllTypes()
    {
        foreach (Assembly assembly in _loadedAssemblies)
        {
            try
            {
                CacheAssemblyAttributes(assembly);
                Type[] types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (type != null && !IsSystemOrAvaloniaType(type))
                    {
                        _typeByName.TryAdd(type.FullName ?? type.Name, type);
                        
                        _typeByName.TryAdd(type.Name, type);
                        CacheTypeHierarchy(type);
                        CacheTypeAttributes(type);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (Type? type in ex.Types)
                {
                    if (type != null && !IsSystemOrAvaloniaType(type))
                    {
                        _typeByName.TryAdd(type.FullName ?? type.Name, type);
                        _typeByName.TryAdd(type.Name, type);
                        CacheTypeHierarchy(type);
                        CacheTypeAttributes(type);
                    }
                }
            }
            catch
            {
                // Ignore posible problematic assemblies
            }
        }
    }
    
    private bool IsSystemOrAvaloniaType(Type type)
    {
        string? namespaceName = type.Namespace;
        if (string.IsNullOrEmpty(namespaceName)) return false;
        string[] systemNamespaces = new[]
        {
            "System",
            "Microsoft",
            "Avalonia"
        };
        return systemNamespaces.Any(ns => namespaceName.StartsWith(ns, StringComparison.OrdinalIgnoreCase));
    }
    
    private void CacheAssemblyAttributes(Assembly assembly)
    {
        try
        {
            object[] customAttributes = assembly.GetCustomAttributes(false);
            List<(Type AttributeType, object Attribute, Type? FeatureType)> assemblyAttributesList = new List<(Type AttributeType, object Attribute, Type? FeatureType)>();
            foreach (object attribute in customAttributes)
            {
                Type attributeType = attribute.GetType();
                Type? featureType = null;
                PropertyInfo[] properties = attributeType.GetProperties();
                foreach (PropertyInfo prop in properties)
                {
                    if (prop.PropertyType == typeof(Type))
                    {
                        featureType = prop.GetValue(attribute) as Type;
                        break;
                    }
                }
                
                if (featureType == null)
                {
                    ConstructorInfo[] constructors = attributeType.GetConstructors();
                    foreach (ConstructorInfo ctor in constructors)
                    {
                        ParameterInfo[] parameters = ctor.GetParameters();
                        if (parameters.Length > 0 && parameters[0].ParameterType == typeof(Type))
                        {
                            try
                            {
                                FieldInfo[] fields = attributeType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                                FieldInfo? typeField = fields.FirstOrDefault(f => f.FieldType == typeof(Type));
                                if (typeField != null)
                                {
                                    featureType = typeField.GetValue(attribute) as Type;
                                }
                            }
                            catch
                            {
                                // Continue without feature type
                            }
                            break;
                        }
                    }
                }
                assemblyAttributesList.Add((attributeType, attribute, featureType));
            }
            if (assemblyAttributesList.Any())
            {
                _assemblyAttributes.TryAdd(assembly, assemblyAttributesList);
            }
        }
        catch
        {
            // Ignore
        }
    }

    private void CacheTypeAttributes(Type type)
    {
        try
        {
            object[] customAttributes = type.GetCustomAttributes(false);
            
            foreach (object attribute in customAttributes)
            {
                Type attributeType = attribute.GetType();
                
                if (!IsSystemOrAvaloniaType(attributeType))
                {
                    _typesByAttribute.AddOrUpdate(
                        attributeType,
                        new List<Type> { type },
                        (key, list) => { lock (list) { list.Add(type); } return list; }
                    );
                    _typesWithAttributeData.AddOrUpdate(
                        attributeType,
                        new List<(Type Type, object Attribute)> { (type, attribute) },
                        (key, list) => { lock (list) { list.Add((type, attribute)); } return list; }
                    );
                }
            }
        }
        catch
        {
            // Ignore
        }
    }

    private void CacheTypeHierarchy(Type type)
    {
        Type? baseType = type.BaseType;
        while (baseType != null && baseType != typeof(object))
        {
            if (!IsSystemOrAvaloniaType(baseType))
            {
                _typesByBaseType.AddOrUpdate(
                    baseType,
                    new List<Type> { type },
                    (key, list) => { lock (list) { list.Add(type); } return list; }
                );
            }
            baseType = baseType.BaseType;
        }
        
        foreach (Type interfaceType in type.GetInterfaces())
        {
            if (!IsSystemOrAvaloniaType(interfaceType))
            {
                _typesByInterface.AddOrUpdate(
                    interfaceType,
                    new List<Type> { type },
                    (key, list) => { lock (list) { list.Add(type); } return list; }
                );
            }
        }
    }

    public Type? GetType(string typeName)
    {
        if (!_isInitialized) Initialize();
        
        return _typeByName.TryGetValue(typeName, out Type? type) ? type : null;
    }

    public T? GetType<T>() where T : class
    {
        Type? type = GetType(typeof(T).Name);
        return type as T;
    }

    public IEnumerable<Type> GetTypesDerivedFrom<T>()
    {
        return GetTypesDerivedFrom(typeof(T));
    }
    
    public IEnumerable<Type> GetTypesDerivedFrom(Type baseType)
    {
        if (!_isInitialized) Initialize();
        if (_typesByBaseType.TryGetValue(baseType, out List<Type>? types))
        {
            lock (types)
            {
                return types.ToList();
            }
        }
        return Enumerable.Empty<Type>();
    }

    public IEnumerable<Type> GetTypesImplementing<T>()
    {
        return GetTypesImplementing(typeof(T));
    }

    public IEnumerable<Type> GetTypesImplementing(Type interfaceType)
    {
        if (!_isInitialized) Initialize();
        if (_typesByInterface.TryGetValue(interfaceType, out List<Type>? types))
        {
            lock (types)
            {
                return types.ToList();
            }
        }
        return Enumerable.Empty<Type>();
    }

    public IEnumerable<Assembly> GetAssembliesWithAttribute<T>() where T : Attribute
    {
        return GetAssembliesWithAttribute(typeof(T));
    }

    public IEnumerable<Assembly> GetAssembliesWithAttribute(Type attributeType)
    {
        if (!_isInitialized) Initialize();
        
        return _assemblyAttributes
            .Where(kvp => kvp.Value.Any(attr => attr.AttributeType == attributeType))
            .Select(kvp => kvp.Key);
    }

    public IEnumerable<Type> GetTypesByAssemblyAttribute<T>() where T : Attribute
    {
        return GetTypesByAssemblyAttribute(typeof(T));
    }

    public IEnumerable<Type> GetTypesByAssemblyAttribute(Type attributeType)
    {
        if (!_isInitialized) Initialize();
        
        List<Type> featureTypes = new List<Type>();
        
        foreach (List<(Type AttributeType, object Attribute, Type? FeatureType)> assemblyData in _assemblyAttributes.Values)
        {
            foreach ((Type attrType, object attribute, Type? featureType) in assemblyData)
            {
                if (attrType == attributeType && featureType != null)
                {
                    featureTypes.Add(featureType);
                }
            }
        }
        
        return featureTypes;
    }

    public IEnumerable<Type> FindTypes(string namePattern)
    {
        if (!_isInitialized) Initialize();
        return _typeByName.Values
            .Where(type => type.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase) ||
                          (type.FullName?.Contains(namePattern, StringComparison.OrdinalIgnoreCase) ?? false))
            .Distinct();
    }

    public IEnumerable<Type> GetAllTypes()
    {
        if (!_isInitialized) Initialize();
        
        return _typeByName.Values.Distinct();
    }

    public TypeRegistryStats GetStats()
    {
        if (!_isInitialized) Initialize();
        return new TypeRegistryStats
        {
            LoadedAssembliesCount = _loadedAssemblies.Count,
            CachedTypesCount = _typeByName.Values.Distinct().Count(),
            BaseTypesCount = _typesByBaseType.Count,
            InterfacesCount = _typesByInterface.Count,
            AttributeTypesCount = _typesByAttribute.Count,
            AssemblyAttributesCount = _assemblyAttributes.Count,
        };
    }

    public void Refresh()
    {
        lock (_lock)
        {
            HashSet<Assembly> currentAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToHashSet();
            List<Assembly> newAssemblies = currentAssemblies.Except(_loadedAssemblies).ToList();
            if (newAssemblies.Any())
            {
                _loadedAssemblies.UnionWith(newAssemblies);
                
                foreach (Assembly assembly in newAssemblies)
                {
                    try
                    {
                        Type[] types = assembly.GetTypes();
                        foreach (var type in types)
                        {
                            if (type != null)
                            {
                                _typeByName.TryAdd(type.FullName ?? type.Name, type);
                                _typeByName.TryAdd(type.Name, type);
                                CacheTypeHierarchy(type);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }
    }
}

public class TypeRegistryStats
{
    public int LoadedAssembliesCount { get; set; }
    public int CachedTypesCount { get; set; }
    public int BaseTypesCount { get; set; }
    public int InterfacesCount { get; set; }
    public int AttributeTypesCount { get; set; }
    public int AssemblyAttributesCount { get; set; }
    public int FeatureTypesCount { get; set; }
    
    public override string ToString()
    {
        return $"Assemblies: {LoadedAssembliesCount}, Types: {CachedTypesCount}, BaseTypes: {BaseTypesCount}, " +
               $"Interfaces: {InterfacesCount}, Attributes: {AttributeTypesCount}, " +
               $"AssemblyAttributes: {AssemblyAttributesCount}, Features: {FeatureTypesCount}";
    }
}
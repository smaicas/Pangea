using System.Collections.Concurrent;
using System.Reflection;

namespace CdCSharp.Pangea.Core.Base;

/// <summary>
/// Caches the application's own types so bootstrap code can find view models, windows and features
/// without rescanning assemblies each time.
/// </summary>
/// <remarks>
/// <para>
/// An ordinary object with an ordinary lifetime: one instance is built during startup and registered
/// in the container, so the scan belongs to the application that asked for it.
/// </para>
/// <para>
/// Scanning is deferred until the first query and happens once. System, Microsoft and Avalonia
/// assemblies are skipped: the registry is about application types.
/// </para>
/// </remarks>
public class TypeRegistry
{
    private static readonly string[] FrameworkAssemblyPrefixes =
    [
        "System.", "Microsoft.", "mscorlib", "netstandard", "Avalonia.",
        "WindowsBase", "PresentationCore", "PresentationFramework"
    ];

    private static readonly string[] FrameworkNamespacePrefixes = ["System", "Microsoft", "Avalonia"];

    private readonly ConcurrentDictionary<string, Type> _typeByName = new();
    private readonly ConcurrentDictionary<Type, List<Type>> _typesByBaseType = new();
    private readonly ConcurrentDictionary<Type, List<Type>> _typesByInterface = new();
    private readonly HashSet<Assembly> _assemblies = [];
    private readonly HashSet<Assembly> _explicitAssemblies;
    private readonly object _initializationLock = new();

    private bool _initialized;

    /// <param name="additionalAssemblies">
    /// Assemblies to scan on top of the ones reachable from the entry assembly. Naming an assembly
    /// here is taken at face value: it is scanned even when the usual framework heuristics would
    /// have skipped it, and so are its types.
    /// </param>
    public TypeRegistry(IEnumerable<Assembly>? additionalAssemblies = null) =>
        _explicitAssemblies = additionalAssemblies?.ToHashSet() ?? [];

    /// <summary>
    /// Scans the application assemblies. Called automatically by the first query; call it directly
    /// to pay the cost at a moment of your choosing.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        lock (_initializationLock)
        {
            if (_initialized) return;

            CollectAssemblies();
            CacheTypes();
            _initialized = true;
        }
    }

    /// <summary>
    /// Finds a type by full name or by simple name. Simple names are ambiguous across namespaces;
    /// the first one scanned wins, so prefer the full name when it matters.
    /// </summary>
    public Type? GetType(string typeName)
    {
        EnsureInitialized();
        return _typeByName.TryGetValue(typeName, out Type? type) ? type : null;
    }

    /// <summary>Types whose name or full name contains <paramref name="namePattern"/>.</summary>
    public IEnumerable<Type> FindTypes(string namePattern)
    {
        EnsureInitialized();

        return _typeByName.Values
            .Where(type => type.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase) ||
                           (type.FullName?.Contains(namePattern, StringComparison.OrdinalIgnoreCase) ?? false))
            .Distinct();
    }

    public IEnumerable<Type> GetTypesDerivedFrom<T>() => GetTypesDerivedFrom(typeof(T));

    public IEnumerable<Type> GetTypesDerivedFrom(Type baseType)
    {
        EnsureInitialized();
        return Snapshot(_typesByBaseType, baseType);
    }

    public IEnumerable<Type> GetTypesImplementing<T>() => GetTypesImplementing(typeof(T));

    public IEnumerable<Type> GetTypesImplementing(Type interfaceType)
    {
        EnsureInitialized();
        return Snapshot(_typesByInterface, interfaceType);
    }

    private void EnsureInitialized()
    {
        if (!_initialized) Initialize();
    }

    private static IEnumerable<Type> Snapshot(ConcurrentDictionary<Type, List<Type>> index, Type key)
    {
        if (!index.TryGetValue(key, out List<Type>? types)) return [];

        lock (types)
        {
            return types.ToList();
        }
    }

    /// <summary>
    /// Walks from the entry assembly outwards through references, so types living in a library the
    /// application depends on are found even when nothing has forced it to load yet.
    /// </summary>
    private void CollectAssemblies()
    {
        HashSet<Assembly> found = [.. _explicitAssemblies];

        foreach (Assembly? candidate in new[] { Assembly.GetEntryAssembly(), Assembly.GetExecutingAssembly() })
        {
            if (candidate is not null) found.Add(candidate);
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!IsFrameworkAssembly(assembly.GetName().Name)) found.Add(assembly);
        }

        HashSet<string> known = found.Select(a => a.GetName().Name ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Queue<Assembly> pending = new(found);

        while (pending.Count > 0)
        {
            AssemblyName[] references;
            try
            {
                references = pending.Dequeue().GetReferencedAssemblies();
            }
            catch (Exception ex) when (ex is FileNotFoundException or BadImageFormatException)
            {
                continue;
            }

            foreach (AssemblyName reference in references)
            {
                if (reference.Name is null || IsFrameworkAssembly(reference.Name) || !known.Add(reference.Name))
                {
                    continue;
                }

                try
                {
                    Assembly loaded = Assembly.Load(reference);
                    if (found.Add(loaded)) pending.Enqueue(loaded);
                }
                catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
                {
                    // A reference that cannot be resolved simply contributes no types.
                }
            }
        }

        _assemblies.UnionWith(found);
    }

    private void CacheTypes()
    {
        foreach (Assembly assembly in _assemblies)
        {
            bool explicitlyRequested = _explicitAssemblies.Contains(assembly);

            foreach (Type type in SafeGetTypes(assembly))
            {
                if (!explicitlyRequested && IsFrameworkType(type)) continue;

                _typeByName.TryAdd(type.FullName ?? type.Name, type);
                _typeByName.TryAdd(type.Name, type);
                CacheHierarchy(type);
            }
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Partially loadable assembly: keep the types that did resolve.
            return ex.Types.OfType<Type>();
        }
    }

    private void CacheHierarchy(Type type)
    {
        Type? baseType = type.BaseType;
        while (baseType is not null && baseType != typeof(object))
        {
            Index(_typesByBaseType, baseType, type);
            baseType = baseType.BaseType;
        }

        foreach (Type interfaceType in type.GetInterfaces())
        {
            Index(_typesByInterface, interfaceType, type);

            // Indexed under the open definition too, so "everything implementing IFoo<>" is a
            // question that can be asked without knowing the type arguments in advance.
            if (interfaceType.IsGenericType)
            {
                Index(_typesByInterface, interfaceType.GetGenericTypeDefinition(), type);
            }
        }
    }

    private static void Index(ConcurrentDictionary<Type, List<Type>> target, Type key, Type type) =>
        target.AddOrUpdate(
            key,
            _ => [type],
            (_, list) =>
            {
                lock (list) { list.Add(type); }
                return list;
            });

    private static bool IsFrameworkAssembly(string? assemblyName) =>
        string.IsNullOrEmpty(assemblyName) ||
        FrameworkAssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsFrameworkType(Type type) =>
        type.Namespace is { Length: > 0 } ns &&
        FrameworkNamespacePrefixes.Any(prefix => ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}

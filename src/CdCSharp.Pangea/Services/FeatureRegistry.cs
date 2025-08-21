using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using CdCSharp.Pangea.Core.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;

namespace CdCSharp.Pangea.Services;

public static class FeatureRegistry
{
    private static readonly ConcurrentDictionary<string, IPangeaFeature> _features = new();

    public static void RegisterFeature(IPangeaFeature feature)
    {
        _features.TryAdd(feature.Name, feature);
    }

    public static bool IsFeatureAvailable(string featureName)
    {
        return _features.ContainsKey(featureName);
    }

    public static T? GetFeature<T>() where T : class, IPangeaFeature
    {
        return _features.Values.OfType<T>().FirstOrDefault();
    }

    public static IPangeaFeature[] GetAllFeatures()
    {
        return _features.Values.ToArray();
    }
    
    public static void ConfigureAllFeatures(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext)
    {
        foreach (IPangeaFeature feature in _features.Values)
        {
            try
            {
                feature.ConfigureApplication(serviceProvider, applicationContext);
                System.Diagnostics.Debug.WriteLine($"ConfigureApplication completed: {feature.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigureApplication error on feature: {feature.Name}: {ex.Message}");
            }
        }
    }
    
    public static void DiscoverAndRegisterFeatures(IServiceCollection services)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Discovering features...");

            LoadAllAvailableAssemblies();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !IsSystemAssembly(assembly))
                .ToArray();

            System.Diagnostics.Debug.WriteLine($"Scanning {assemblies.Length} assemblies for features");

            int featuresRegistered = 0;

            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type[] featureTypes = assembly.GetTypes()
                        .Where(type => typeof(IPangeaFeature).IsAssignableFrom(type))
                        .Where(type => !type.IsAbstract && !type.IsInterface)
                        .ToArray();

                    foreach (Type featureType in featureTypes)
                    {
                        if (ProcessFeatureType(featureType, services))
                            featuresRegistered++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error scanning assembly: {ex.GetType().Name}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Registered features: {featuresRegistered}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DiscoverAndRegisterFeatures error: {ex.Message}");
        }
    }

    private static bool ProcessFeatureType(Type featureType, IServiceCollection services)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Processing feature: {featureType.FullName}");

            if (_features.Values.Any(f => f.GetType() == featureType))
            {
                System.Diagnostics.Debug.WriteLine($"Feature registered yet: {featureType.Name}");
                return false;
            }

            if (Activator.CreateInstance(featureType) is IPangeaFeature featureInstance)
            {
                featureInstance.ConfigureServices(services);
                RegisterFeature(featureInstance);

                System.Diagnostics.Debug.WriteLine($"Registered feature: {featureInstance.Name} v{featureInstance.Version}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error while processing {featureType.Name}: {ex.Message}");
            return false;
        }
    }

    private static void LoadAllAvailableAssemblies()
    {
        try
        {
            HashSet<string> processedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name ?? "")
                .Where(name => !string.IsNullOrEmpty(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            LoadAssembliesFromApplicationDirectory(processedAssemblies);

            System.Diagnostics.Debug.WriteLine($"Total processed assemblies: {processedAssemblies.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadAllAvailableAssemblies error: {ex.Message}");
        }
    }

    private static void LoadAssembliesFromApplicationDirectory(HashSet<string> processedAssemblies)
    {
        try
        {
            string applicationDirectory = AppContext.BaseDirectory;
            System.Diagnostics.Debug.WriteLine($"Searching assemblies in: {applicationDirectory}");

            if (!Directory.Exists(applicationDirectory))
                return;

            string[] dllFiles = Directory.GetFiles(applicationDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            
            System.Diagnostics.Debug.WriteLine($"Found {dllFiles.Length} .dll files");

            foreach (string dllFile in dllFiles)
            {
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(dllFile);
                    
                    if (processedAssemblies.Contains(fileName) || IsSystemAssemblyName(fileName))
                        continue;

                    Assembly assembly = Assembly.LoadFrom(dllFile);
                    
                    if (processedAssemblies.Add(fileName))
                    {
                        System.Diagnostics.Debug.WriteLine($"Loaded from file: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Can't load {Path.GetFileName(dllFile)}: {ex.GetType().Name}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadAssembliesFromApplicationDirectory error: {ex.Message}");
        }
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        string? assemblyName = assembly.GetName().Name;
        return IsSystemAssemblyName(assemblyName);
    }

    private static bool IsSystemAssemblyName(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return true;

        return assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.Equals("Avalonia", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Avalonia.", StringComparison.OrdinalIgnoreCase);
    }
}
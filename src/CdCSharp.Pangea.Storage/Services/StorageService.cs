using CdCSharp.Pangea.Storage.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CdCSharp.Pangea.Storage.Services;

/// <summary>
/// File access rooted at the platform's per-application folders.
/// </summary>
/// <remarks>
/// Writes create the folders they need; reads do not touch the filesystem beyond reading. Having
/// <c>ReadTextAsync</c> create directories was a surprise with real consequences: asking for a file
/// that is not there left an empty folder behind.
/// </remarks>
public class StorageService : IStorageService
{
    private readonly IPlatformPathProvider _pathProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public StorageService(IPlatformPathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _jsonOptions = CreateSerializerOptions();
    }

    /// <summary>
    /// The JSON settings the service reads and writes with: camel case, indented, computed
    /// properties left out.
    /// </summary>
    /// <remarks>
    /// Public so a double standing in for the service - the in-memory one in the testing package
    /// among them - produces the same files. A new instance each call, because options are frozen
    /// on first use and a shared one would be a trap for whoever tries to change it.
    /// </remarks>
    public static JsonSerializerOptions CreateSerializerOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { IgnoreComputedProperties } }
    };

    /// <summary>
    /// Leaves out properties that are computed from others: a getter with no setter, no
    /// constructor parameter and no <see cref="JsonIncludeAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A model the UI edits - <c>Total => Quantity * Price</c>, <c>Summary</c>, <c>IsValid</c> -
    /// would otherwise write every one of them to the file. They are never read back, so the file
    /// fills with values that look like state and are not, and go stale the moment the code that
    /// computes them changes.
    /// </para>
    /// <para>
    /// Narrower than <see cref="JsonSerializerOptions.IgnoreReadOnlyProperties"/>, which would also
    /// drop a getter-only property set through the constructor and break the round trip of an
    /// immutable type. A getter-only collection the type asks to have populated stays too.
    /// </para>
    /// </remarks>
    private static void IgnoreComputedProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            JsonPropertyInfo property = typeInfo.Properties[i];

            bool computed = property.Get is not null
                && property.Set is null
                && property.AssociatedParameter is null
                && (property.ObjectCreationHandling ?? typeInfo.PreferredPropertyObjectCreationHandling)
                    != JsonObjectCreationHandling.Populate
                && property.AttributeProvider?.IsDefined(typeof(JsonIncludeAttribute), inherit: true) != true;

            if (computed) typeInfo.Properties.RemoveAt(i);
        }
    }

    public string GetApplicationDataPath() => _pathProvider.GetApplicationDataPath();

    public string GetUserDataPath() => _pathProvider.GetUserDataPath();

    public string GetTempPath() => _pathProvider.GetTempPath();

    public string GetCachePath() => _pathProvider.GetCachePath();

    /// <summary>
    /// The full path of a file inside the application's data folder. Subfolders are allowed;
    /// anything that would land outside the folder is rejected.
    /// </summary>
    /// <remarks>
    /// <see cref="Path.Combine(string, string)"/> answers an absolute second argument by discarding
    /// the first, and says nothing about "..", so a file name taken from user input or from a
    /// document could be written anywhere the process can reach. A method whose whole purpose is to
    /// place a file in the data folder should not be the way out of it.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="fileName"/> is blank, absolute, or escapes the data folder.
    /// </exception>
    public string GetDataFilePath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (Path.IsPathRooted(fileName))
        {
            throw new ArgumentException(
                $"'{fileName}' is an absolute path. GetDataFilePath names a file inside the " +
                "application data folder; pass a relative name, or use the absolute path directly.",
                nameof(fileName));
        }

        string root = Path.GetFullPath(GetApplicationDataPath());
        string combined = Path.GetFullPath(Path.Combine(root, fileName));

        // Compared with a trailing separator so that a sibling folder whose name merely starts the
        // same way - "MyApp.backup" next to "MyApp" - does not read as being inside it.
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"'{fileName}' resolves to '{combined}', which is outside the application data folder.",
                nameof(fileName));
        }

        return combined;
    }

    /// <summary>
    /// Reads a file, with the same semantics as <see cref="File.ReadAllTextAsync(string, CancellationToken)"/>:
    /// a missing file throws <see cref="FileNotFoundException"/>, a missing folder
    /// <see cref="DirectoryNotFoundException"/>.
    /// </summary>
    public Task<string> ReadTextAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return File.ReadAllTextAsync(filePath);
    }

    /// <summary>Writes a file, creating its folder if needed.</summary>
    public Task WriteTextAsync(string filePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        EnsureDirectoryExists(filePath);
        return File.WriteAllTextAsync(filePath, content);
    }

    /// <summary>
    /// Reads and deserializes a file, returning null when it does not exist.
    /// </summary>
    /// <remarks>
    /// Deliberately softer than <see cref="ReadTextAsync"/>: the JSON overloads exist for state that
    /// may legitimately not have been written yet, such as settings on a first run.
    /// </remarks>
    /// <summary>
    /// The stored object, or <see langword="null"/> when the file has never been written.
    /// </summary>
    /// <exception cref="StorageSerializationException">
    /// The file exists and does not hold this type. Distinct from an <see cref="IOException"/>,
    /// which says the file could not be read at all.
    /// </exception>
    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath)) return null;

        string json = await ReadTextAsync(filePath);

        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new StorageSerializationException(
                $"The contents of '{filePath}' are not a {typeof(T).Name}. The file was read; what is in it " +
                "does not match the type it was asked for.",
                filePath, typeof(T), ex);
        }
    }

    /// <summary>
    /// Writes <paramref name="data"/> as JSON.
    /// </summary>
    /// <remarks>
    /// Serialized before the file is touched, so a type that cannot be written leaves whatever was
    /// there intact rather than truncating it.
    /// </remarks>
    /// <exception cref="StorageSerializationException">
    /// <paramref name="data"/> cannot be turned into JSON. This is a defect in the application
    /// rather than a condition of the machine: it will fail the same way on every run, so catching
    /// it alongside <see cref="IOException"/> - the "try again later" case - is how an application
    /// ends up discarding everything it meant to save.
    /// </exception>
    public Task WriteJsonAsync<T>(string filePath, T data) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        string json;

        try
        {
            json = JsonSerializer.Serialize(data, _jsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new StorageSerializationException(
                $"A {typeof(T).Name} could not be turned into JSON for '{filePath}', so nothing was written. " +
                "This is the same failure on every run: the type cannot be serialized as it stands.",
                filePath, typeof(T), ex);
        }

        return WriteTextAsync(filePath, json);
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    public bool DirectoryExists(string directoryPath) => Directory.Exists(directoryPath);

    public void CreateDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);
    }

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    public void DeleteDirectory(string directoryPath, bool recursive = false)
    {
        if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, recursive);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }
}

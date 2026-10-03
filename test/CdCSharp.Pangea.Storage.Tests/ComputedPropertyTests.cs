using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Services;
using System.Text.Json.Serialization;

namespace CdCSharp.Pangea.Storage.Tests;

/// <summary>
/// What a stored object puts in its file: its state, not what is computed from it.
/// </summary>
/// <remarks>
/// A model the UI edits carries computed properties for the screen to bind to. Written as they
/// were, they filled settings files with values that were never read back and went stale as soon as
/// the code computing them changed.
/// </remarks>
public class ComputedPropertyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-computed-" + Guid.NewGuid().ToString("N"));
    private readonly StorageService _storage;

    public ComputedPropertyTests() => _storage = new StorageService(new TempPathProvider(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    public sealed class Step : ObservableModel
    {
        private string _key = "A";
        private int _repeat = 1;

        public string Key { get => _key; set => SetProperty(ref _key, value); }
        public int Repeat { get => _repeat; set => SetProperty(ref _repeat, value); }

        public string Summary => $"{Key} x{Repeat}";
    }

    /// <summary>Immutable: getter-only, set through the constructor.</summary>
    public sealed class Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public int Sum => X + Y;
    }

    public sealed class Explicit
    {
        public int Value { get; set; }

        [JsonInclude]
        public int Doubled => Value * 2;
    }

    public sealed class Populated
    {
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public List<string> Tags { get; } = [];
    }

    [Fact]
    public async Task AComputedProperty_IsNotWritten()
    {
        string filePath = _storage.GetDataFilePath("step.json");

        await _storage.WriteJsonAsync(filePath, new Step { Key = "B", Repeat = 2 });

        string json = await _storage.ReadTextAsync(filePath);
        Assert.Contains("\"key\"", json, StringComparison.Ordinal);
        Assert.Contains("\"repeat\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("summary", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnObservableModel_RoundTrips()
    {
        string filePath = _storage.GetDataFilePath("step.json");

        await _storage.WriteJsonAsync(filePath, new Step { Key = "B", Repeat = 2 });
        Step? read = await _storage.ReadJsonAsync<Step>(filePath);

        Assert.NotNull(read);
        Assert.Equal("B x2", read.Summary);
    }

    /// <summary>
    /// The reason IgnoreReadOnlyProperties is not used: it would drop X and Y, and the type would
    /// come back as (0, 0).
    /// </summary>
    [Fact]
    public async Task AGetterOnlyPropertySetThroughTheConstructor_IsKept()
    {
        string filePath = _storage.GetDataFilePath("point.json");

        await _storage.WriteJsonAsync(filePath, new Point(3, 4));

        string json = await _storage.ReadTextAsync(filePath);
        Assert.DoesNotContain("sum", json, StringComparison.Ordinal);

        Point? read = await _storage.ReadJsonAsync<Point>(filePath);
        Assert.NotNull(read);
        Assert.Equal(3, read.X);
        Assert.Equal(4, read.Y);
    }

    [Fact]
    public async Task JsonInclude_KeepsAComputedProperty()
    {
        string filePath = _storage.GetDataFilePath("explicit.json");

        await _storage.WriteJsonAsync(filePath, new Explicit { Value = 5 });

        Assert.Contains("\"doubled\": 10", await _storage.ReadTextAsync(filePath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGetterOnlyCollectionMarkedForPopulation_RoundTrips()
    {
        string filePath = _storage.GetDataFilePath("populated.json");
        Populated written = new();
        written.Tags.Add("one");

        await _storage.WriteJsonAsync(filePath, written);
        Populated? read = await _storage.ReadJsonAsync<Populated>(filePath);

        Assert.NotNull(read);
        Assert.Equal(["one"], read.Tags);
    }

    private sealed class TempPathProvider(string root) : IPlatformPathProvider
    {
        public string GetApplicationDataPath() => root;
        public string GetUserDataPath() => root;
        public string GetTempPath() => root;
        public string GetCachePath() => root;
    }
}

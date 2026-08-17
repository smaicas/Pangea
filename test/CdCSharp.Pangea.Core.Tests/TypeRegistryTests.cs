using CdCSharp.Pangea.Core.Base;
using System.Text.Json;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// The registry is what bootstrap uses to find view models, windows and features, so its lookups
/// are load-bearing. Each test builds its own.
/// </summary>
public class TypeRegistryTests
{
    // Fixtures live in the test assembly, which is the entry assembly and therefore always scanned.
    private abstract class Shape;

    private class Square : Shape;

    private sealed class ColouredSquare : Square;

    private interface IDrawable;

    private sealed class Sprite : IDrawable;

    [Fact]
    public void FindsATypeBySimpleName()
    {
        TypeRegistry registry = new();

        Assert.Equal(typeof(Square), registry.GetType(nameof(Square)));
    }

    [Fact]
    public void FindsATypeByFullName()
    {
        TypeRegistry registry = new();

        Assert.Equal(typeof(Square), registry.GetType(typeof(Square).FullName!));
    }

    [Fact]
    public void UnknownTypeName_ReturnsNull()
    {
        TypeRegistry registry = new();

        Assert.Null(registry.GetType("NoSuchTypeAnywhere"));
    }

    [Fact]
    public void QueryingWithoutCallingInitialize_ScansOnDemand()
    {
        // Callers should not have to remember to prime it.
        TypeRegistry registry = new();

        Assert.NotNull(registry.GetType(nameof(Sprite)));
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        TypeRegistry registry = new();

        registry.Initialize();
        registry.Initialize();

        // A second scan would duplicate every entry in the hierarchy indexes.
        Assert.Single(registry.GetTypesDerivedFrom<Square>());
    }

    [Fact]
    public void FindsDirectAndTransitiveSubclasses()
    {
        TypeRegistry registry = new();

        List<Type> shapes = registry.GetTypesDerivedFrom<Shape>().ToList();

        Assert.Contains(typeof(Square), shapes);
        Assert.Contains(typeof(ColouredSquare), shapes);
    }

    [Fact]
    public void FindsImplementationsOfAnInterface()
    {
        TypeRegistry registry = new();

        Assert.Contains(typeof(Sprite), registry.GetTypesImplementing<IDrawable>());
    }

    [Fact]
    public void FindTypes_MatchesOnSubstringIgnoringCase()
    {
        TypeRegistry registry = new();

        List<Type> matches = registry.FindTypes("colouredsq").ToList();

        Assert.Contains(typeof(ColouredSquare), matches);
    }

    [Fact]
    public void FrameworkTypes_AreSkippedByDefault()
    {
        // Indexing the BCL would cost a lot and answer questions nobody asks.
        TypeRegistry registry = new();

        Assert.Null(registry.GetType(nameof(JsonSerializer)));
    }

    [Fact]
    public void AnExplicitlyNamedAssembly_IsScannedEvenIfTheHeuristicsWouldSkipIt()
    {
        // The supported way to widen the scan: name the assembly and it is taken at face value.
        TypeRegistry registry = new([typeof(JsonSerializer).Assembly]);

        Assert.Equal(typeof(JsonSerializer), registry.GetType(nameof(JsonSerializer)));
    }

    [Fact]
    public void TwoRegistries_DoNotShareState()
    {
        TypeRegistry withJson = new([typeof(JsonSerializer).Assembly]);
        TypeRegistry withoutJson = new();

        Assert.NotNull(withJson.GetType(nameof(JsonSerializer)));
        Assert.Null(withoutJson.GetType(nameof(JsonSerializer)));
    }
}

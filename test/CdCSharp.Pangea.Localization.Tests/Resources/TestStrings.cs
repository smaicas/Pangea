using System.Resources;

namespace CdCSharp.Pangea.Localization.Tests.Resources;

/// <summary>
/// Hand-written stand-in for a .resx designer class: what the localization service looks for is a
/// type exposing a public static <see cref="System.Resources.ResourceManager"/> property.
/// </summary>
public static class TestStrings
{
    public static ResourceManager ResourceManager { get; } =
        new("CdCSharp.Pangea.Localization.Tests.Resources.TestStrings", typeof(TestStrings).Assembly);
}

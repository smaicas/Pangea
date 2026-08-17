using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Providers;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Tests;

/// <summary>
/// Where a Pangea application puts the user's data. Every provider is exercised on whatever machine
/// runs the suite: they only compose paths, so none of them needs to be on its own operating system
/// to be checked, and a rule that only holds on one platform is worth knowing about.
/// </summary>
public class PlatformPathProviderTests
{
    private const string AppName = "PathProbeApp";

    public static TheoryData<string> ProviderNames() => ["windows", "linux", "macos", "portable"];

    private static IPlatformPathProvider Create(string provider, StorageOptions? options = null)
    {
        IOptions<StorageOptions> wrapped = Options.Create(options ?? new StorageOptions { ApplicationName = AppName });

        return provider switch
        {
            "windows" => new WindowsPlatformPathProvider(wrapped),
            "linux" => new LinuxPlatformPathProvider(wrapped),
            "macos" => new MacOSPlatformPathProvider(wrapped),
            "portable" => new PortablePlatformPathProvider(wrapped),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    private static IEnumerable<string> AllPaths(IPlatformPathProvider provider) =>
    [
        provider.GetApplicationDataPath(),
        provider.GetUserDataPath(),
        provider.GetTempPath(),
        provider.GetCachePath()
    ];

    [Theory]
    [MemberData(nameof(ProviderNames))]
    public void EveryPathIsRootedAndNonEmpty(string provider)
    {
        foreach (string path in AllPaths(Create(provider)))
        {
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path), $"'{path}' is not rooted.");
        }
    }

    /// <summary>
    /// Two applications must not share a data directory. The portable provider separates by the
    /// directory it runs from rather than by name, which is the point of portable mode.
    /// </summary>
    [Theory]
    [InlineData("windows")]
    [InlineData("linux")]
    [InlineData("macos")]
    public void EveryPathIsScopedToTheApplicationName(string provider)
    {
        foreach (string path in AllPaths(Create(provider)))
        {
            Assert.Contains(AppName, path, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(ProviderNames))]
    public void TheFourPathsAreDistinct(string provider)
    {
        string[] paths = AllPaths(Create(provider)).ToArray();

        Assert.Equal(paths.Length, paths.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The documented escape hatch. It redirects application data only - user data, temp and cache
    /// keep their platform locations - so it is pinned here rather than left to be discovered.
    /// </summary>
    [Theory]
    [MemberData(nameof(ProviderNames))]
    public void CustomDataPath_RedirectsApplicationDataAndNothingElse(string provider)
    {
        string custom = Path.Combine(Path.GetTempPath(), "pangea-custom-root");

        IPlatformPathProvider configured = Create(provider,
            new StorageOptions { ApplicationName = AppName, CustomDataPath = custom });

        IPlatformPathProvider plain = Create(provider);

        Assert.Equal(custom, configured.GetApplicationDataPath());
        Assert.Equal(plain.GetUserDataPath(), configured.GetUserDataPath());
        Assert.Equal(plain.GetTempPath(), configured.GetTempPath());
        Assert.Equal(plain.GetCachePath(), configured.GetCachePath());
    }

    [Theory]
    [MemberData(nameof(ProviderNames))]
    public void AnEmptyCustomDataPath_IsIgnored(string provider)
    {
        IPlatformPathProvider blank = Create(provider,
            new StorageOptions { ApplicationName = AppName, CustomDataPath = "" });

        Assert.Equal(Create(provider).GetApplicationDataPath(), blank.GetApplicationDataPath());
    }

    [Fact]
    public void PortableModeKeepsEverythingTogether()
    {
        IPlatformPathProvider portable = Create("portable");

        string root = Path.GetDirectoryName(portable.GetUserDataPath())!;

        Assert.Equal(root, Path.GetDirectoryName(portable.GetTempPath()));
        Assert.Equal(root, Path.GetDirectoryName(portable.GetCachePath()));
    }
}

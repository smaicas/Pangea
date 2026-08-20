using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Security;
using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Storage.Providers;
using CdCSharp.Pangea.Storage.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Where a credential goes when the application has not been given a platform keystore.
/// </summary>
/// <remarks>
/// The point of the type is that it is never a plain file in the settings folder. What it can
/// promise differs by platform - real encryption on Windows, permissions elsewhere - and it says
/// which through <see cref="ISecretStore.Protection"/> rather than leaving an application to guess.
/// </remarks>
public class SecretStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-secrets", Guid.NewGuid().ToString("N"));
    private readonly FileSecretStore _store;

    public SecretStoreTests()
    {
        Directory.CreateDirectory(_root);

        StorageOptions options = new() { UsePortableMode = true, CustomDataPath = _root };

        _store = new FileSecretStore(
            new StorageService(new PortablePlatformPathProvider(Options.Create(options))),
            NullLogger<FileSecretStore>.Instance);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A temporary directory that outlives the test is litter, not a failure.
        }
    }

    [Fact]
    public async Task ASecretComesBackAsItWentIn()
    {
        await _store.SetAsync("refresh-token", "the-token", TestContext.Current.CancellationToken);

        Assert.Equal("the-token", await _store.GetAsync("refresh-token", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AKeyThatWasNeverStored_HasNoSecret()
    {
        Assert.Null(await _store.GetAsync("never-set", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoringTwice_KeepsTheSecondOne()
    {
        await _store.SetAsync("session", "first", TestContext.Current.CancellationToken);
        await _store.SetAsync("session", "second", TestContext.Current.CancellationToken);

        Assert.Equal("second", await _store.GetAsync("session", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemovingASecret_TakesItAway_AndRemovingItAgainIsFine()
    {
        await _store.SetAsync("session", "the-token", TestContext.Current.CancellationToken);

        await _store.RemoveAsync("session", TestContext.Current.CancellationToken);
        await _store.RemoveAsync("session", TestContext.Current.CancellationToken);

        Assert.Null(await _store.GetAsync("session", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The whole reason for the type: what lands on disk is not the credential, and the file is not
    /// sitting in the settings folder with the key in its name.
    /// </summary>
    [Fact]
    public async Task WhatIsOnDisk_IsNotTheSecretItself()
    {
        await _store.SetAsync("refresh-token", "sensitive-value", TestContext.Current.CancellationToken);

        string[] files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        string written = Assert.Single(files);

        Assert.DoesNotContain("refresh-token", Path.GetFileName(written), StringComparison.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows()) return;

        // Windows encrypts under the current user; elsewhere the filesystem is all there is.
        Assert.Equal(SecretProtection.OperatingSystem, _store.Protection);
        Assert.DoesNotContain(
            "sensitive-value",
            await File.ReadAllTextAsync(written, TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheStoreSaysWhatItCanPromise()
    {
        Assert.Equal(
            OperatingSystem.IsWindows() ? SecretProtection.OperatingSystem : SecretProtection.UserOnly,
            _store.Protection);
    }
}

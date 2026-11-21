﻿using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Providers;
using CdCSharp.Pangea.Storage.Services;
using CdCSharp.Pangea.Supabase.Services;
using CdCSharp.Pangea.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Supabase.Gotrue;

namespace CdCSharp.Pangea.Supabase.Tests;

/// <summary>
/// The session that has to be there on the next launch, when there is no secret store to keep it.
/// </summary>
/// <remarks>
/// Mostly written against real files, because the thing being checked is that what one launch
/// writes the next one reads - and the in-memory double would prove that about itself. The double
/// is used where the question is the opposite one: that nothing goes around the storage service.
/// </remarks>
public sealed class SessionPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-session-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IStorageService RealStorage()
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            ApplicationName = "SessionProbe",
            CustomDataPath = _root
        });

        return new StorageService(new PortablePlatformPathProvider(options));
    }

    /// <summary>A launch: the persistence as the client provider builds it, primed before use.</summary>
    private static async Task<StorageSessionPersistence> LaunchAsync(IStorageService storage, string fileName = "session.json")
    {
        StorageSessionPersistence persistence = new(storage, fileName, NullLogger.Instance);

        await persistence.PrimeAsync(Ct);

        return persistence;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task WithNothingStored_ThereIsNoSessionToRestore() =>
        Assert.Null((await LaunchAsync(RealStorage())).LoadSession());

    [Fact]
    public async Task ASavedSessionComesBack_OnTheNextLaunch()
    {
        IStorageService storage = RealStorage();
        StorageSessionPersistence first = await LaunchAsync(storage);

        first.SaveSession(new Session
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            User = new User { Id = "8a1f", IsAnonymous = true }
        });

        // Gotrue reads back what it just saved before the write has landed.
        Assert.Equal("refresh", first.LoadSession()?.RefreshToken);

        await first.DrainAsync();

        Session restored = Assert.IsType<Session>((await LaunchAsync(storage)).LoadSession());

        Assert.Equal("refresh", restored.RefreshToken);
        Assert.Equal("8a1f", restored.User?.Id);
        Assert.True(restored.User?.IsAnonymous);
    }

    /// <summary>
    /// Signing out has to take the credential with it. A refresh token that outlives the sign-out is
    /// the account still being reachable by anything that can read the file.
    /// </summary>
    [Fact]
    public async Task DestroyingTheSessionRemovesTheStoredCredential()
    {
        IStorageService storage = RealStorage();
        StorageSessionPersistence persistence = await LaunchAsync(storage);

        persistence.SaveSession(new Session { RefreshToken = "refresh" });
        persistence.DestroySession();
        await persistence.DrainAsync();

        Assert.Null(persistence.LoadSession());
        Assert.Null((await LaunchAsync(storage)).LoadSession());
        Assert.Empty(Directory.EnumerateFiles(_root, "session.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DestroyingASessionThatWasNeverSaved_DoesNothing()
    {
        StorageSessionPersistence persistence = await LaunchAsync(RealStorage());

        persistence.DestroySession();
        await persistence.DrainAsync();

        Assert.Null(persistence.LoadSession());
    }

    /// <summary>
    /// Unreadable is treated as absent: the application signs in again, which is the recovery it
    /// already has for a first run.
    /// </summary>
    [Fact]
    public async Task AStoredSessionThatCannotBeParsed_IsTreatedAsNone()
    {
        IStorageService storage = RealStorage();
        StorageSessionPersistence first = await LaunchAsync(storage);

        first.SaveSession(new Session { RefreshToken = "refresh" });
        await first.DrainAsync();

        string path = Directory.EnumerateFiles(_root, "session.json", SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(path, "{ this is not a session", Ct);

        Assert.Null((await LaunchAsync(storage)).LoadSession());
    }

    [Fact]
    public async Task SavingTwiceKeepsTheLatestSession()
    {
        IStorageService storage = RealStorage();
        StorageSessionPersistence persistence = await LaunchAsync(storage);

        persistence.SaveSession(new Session { RefreshToken = "first" });
        persistence.SaveSession(new Session { RefreshToken = "second" });
        await persistence.DrainAsync();

        Assert.Equal("second", (await LaunchAsync(storage)).LoadSession()?.RefreshToken);
    }

    /// <summary>
    /// The session goes where the storage service puts it, and nowhere else. Written straight to the
    /// path the service names, the in-memory double's session landed in a real file at the root of
    /// the current drive - one file for every container in the process, so that every one of them
    /// restored the same account.
    /// </summary>
    [Fact]
    public async Task TheSessionGoesThroughTheStorageService_AndNeverAroundIt()
    {
        InMemoryStorageService one = new();
        InMemoryStorageService other = new();

        StorageSessionPersistence first = await LaunchAsync(one, "supabase-session.json");
        first.SaveSession(new Session { RefreshToken = "only-mine" });
        await first.DrainAsync();

        string path = one.GetDataFilePath("supabase-session.json");

        Assert.Contains(path, one.Files.Keys);
        Assert.False(File.Exists(path), $"The session was written to the disk at {path}");
        Assert.Null((await LaunchAsync(other, "supabase-session.json")).LoadSession());
        Assert.Equal("only-mine", (await LaunchAsync(one, "supabase-session.json")).LoadSession()?.RefreshToken);
    }
}

using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Providers;
using CdCSharp.Pangea.Storage.Services;
using CdCSharp.Pangea.Supabase.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Supabase.Gotrue;

namespace CdCSharp.Pangea.Supabase.Tests;

/// <summary>
/// The session that has to be there on the next launch.
/// </summary>
/// <remarks>
/// Written against real files rather than the in-memory double, because the thing being checked is
/// that a synchronous contract writes something the same code can read back - and the double would
/// prove that about itself.
/// </remarks>
public sealed class SessionPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-session-" + Guid.NewGuid().ToString("N"));

    private StorageSessionPersistence Arrange(string fileName = "session.json")
    {
        IOptions<StorageOptions> options = Options.Create(new StorageOptions
        {
            ApplicationName = "SessionProbe",
            CustomDataPath = _root
        });

        IStorageService storage = new StorageService(new PortablePlatformPathProvider(options));

        return new StorageSessionPersistence(storage, fileName, NullLogger.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void WithNothingStored_ThereIsNoSessionToRestore() => Assert.Null(Arrange().LoadSession());

    [Fact]
    public void ASavedSessionComesBack()
    {
        StorageSessionPersistence persistence = Arrange();

        persistence.SaveSession(new Session
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            User = new User { Id = "8a1f", IsAnonymous = true }
        });

        Session restored = Assert.IsType<Session>(persistence.LoadSession());

        Assert.Equal("refresh", restored.RefreshToken);
        Assert.Equal("8a1f", restored.User?.Id);
        Assert.True(restored.User?.IsAnonymous);
    }

    /// <summary>
    /// Signing out has to take the credential with it. A refresh token that outlives the sign-out is
    /// the account still being reachable by anything that can read the file.
    /// </summary>
    [Fact]
    public void DestroyingTheSessionRemovesTheStoredCredential()
    {
        StorageSessionPersistence persistence = Arrange();

        persistence.SaveSession(new Session { RefreshToken = "refresh" });
        persistence.DestroySession();

        Assert.Null(persistence.LoadSession());
        Assert.Empty(Directory.EnumerateFiles(_root, "session.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void DestroyingASessionThatWasNeverSaved_DoesNothing()
    {
        StorageSessionPersistence persistence = Arrange();

        persistence.DestroySession();

        Assert.Null(persistence.LoadSession());
    }

    /// <summary>
    /// Unreadable is treated as absent: the application signs in again, which is the recovery it
    /// already has for a first run.
    /// </summary>
    [Fact]
    public void AStoredSessionThatCannotBeParsed_IsTreatedAsNone()
    {
        StorageSessionPersistence persistence = Arrange();

        persistence.SaveSession(new Session { RefreshToken = "refresh" });

        string path = Directory.EnumerateFiles(_root, "session.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(path, "{ this is not a session");

        Assert.Null(persistence.LoadSession());
    }

    [Fact]
    public void SavingTwiceKeepsTheLatestSession()
    {
        StorageSessionPersistence persistence = Arrange();

        persistence.SaveSession(new Session { RefreshToken = "first" });
        persistence.SaveSession(new Session { RefreshToken = "second" });

        Assert.Equal("second", persistence.LoadSession()?.RefreshToken);
    }
}

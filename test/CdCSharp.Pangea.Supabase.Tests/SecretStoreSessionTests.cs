using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Supabase.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Supabase.Gotrue;

namespace CdCSharp.Pangea.Supabase.Tests;

/// <summary>
/// Where the signed-in session lives now.
/// </summary>
/// <remarks>
/// It holds a refresh token, which is a bearer credential: whoever has it is the user until it is
/// revoked. The file it used to live in travels into backups, sync folders and support bundles
/// without anyone deciding that it should.
/// </remarks>
public sealed class SecretStoreSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-secret-session-" + Guid.NewGuid().ToString("N"));

    private const string Key = "test.session";

    private sealed class MemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = [];

        public SecretProtection Protection => SecretProtection.Device;

        public int Writes { get; private set; }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.TryGetValue(key, out string? found) ? found : null);

        public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default)
        {
            _secrets[key] = secret;
            Writes++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }

    public SecretStoreSessionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static Session ASession(string refreshToken = "refresh-me") =>
        new() { AccessToken = "access", RefreshToken = refreshToken };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithNothingStored_ThereIsNoSessionToRestore()
    {
        SecretStoreSessionPersistence persistence = new(new MemorySecretStore(), Key, NullLogger.Instance);

        await persistence.PrimeAsync(legacyFilePath: null, Ct);

        Assert.Null(persistence.LoadSession());
    }

    [Fact]
    public async Task WhatWasSaved_IsWhatTheNextLaunchRestores()
    {
        MemorySecretStore secrets = new();
        SecretStoreSessionPersistence writing = new(secrets, Key, NullLogger.Instance);

        await writing.PrimeAsync(legacyFilePath: null, Ct);

        writing.SaveSession(ASession());
        await writing.DrainAsync();

        // A second run of the application, reading what the first one left.
        SecretStoreSessionPersistence reading = new(secrets, Key, NullLogger.Instance);
        await reading.PrimeAsync(legacyFilePath: null, Ct);

        Assert.Equal("refresh-me", reading.LoadSession()?.RefreshToken);
    }

    [Fact]
    public async Task SigningOut_TakesTheCredentialOutOfTheStore()
    {
        MemorySecretStore secrets = new();
        SecretStoreSessionPersistence persistence = new(secrets, Key, NullLogger.Instance);

        await persistence.PrimeAsync(legacyFilePath: null, Ct);

        persistence.SaveSession(ASession());
        persistence.DestroySession();
        await persistence.DrainAsync();

        Assert.Null(persistence.LoadSession());
        Assert.Null(await secrets.GetAsync(Key, Ct));
    }

    /// <summary>
    /// The upgrade path. Leaving the file alone would sign every existing user out, and - worse -
    /// leave the credential that signed them in exactly where it always was.
    /// </summary>
    [Fact]
    public async Task ASessionLeftByAnOlderVersion_IsMovedIntoTheStoreAndTheFileIsDeleted()
    {
        MemorySecretStore secrets = new();
        string legacy = Path.Combine(_root, "supabase-session.json");

        await File.WriteAllTextAsync(
            legacy, Newtonsoft.Json.JsonConvert.SerializeObject(ASession("from-the-old-file")), Ct);

        SecretStoreSessionPersistence persistence = new(secrets, Key, NullLogger.Instance);

        await persistence.PrimeAsync(legacy, Ct);

        Assert.Equal("from-the-old-file", persistence.LoadSession()?.RefreshToken);
        Assert.NotNull(await secrets.GetAsync(Key, Ct));
        Assert.False(File.Exists(legacy), "The plain file is the thing this feature exists to get rid of.");
    }

    /// <summary>
    /// A store that already holds a session wins: the file is whatever an older version left and is
    /// not authoritative.
    /// </summary>
    [Fact]
    public async Task WithASessionInBothPlaces_TheStoreIsTheOneUsed()
    {
        MemorySecretStore secrets = new();
        string legacy = Path.Combine(_root, "supabase-session.json");

        await secrets.SetAsync(Key, Newtonsoft.Json.JsonConvert.SerializeObject(ASession("current")), Ct);
        await File.WriteAllTextAsync(
            legacy, Newtonsoft.Json.JsonConvert.SerializeObject(ASession("stale")), Ct);

        SecretStoreSessionPersistence persistence = new(secrets, Key, NullLogger.Instance);

        await persistence.PrimeAsync(legacy, Ct);

        Assert.Equal("current", persistence.LoadSession()?.RefreshToken);
    }

    /// <summary>
    /// Gotrue saves from wherever it happens to be, including the UI thread, and never waits. The
    /// writes still have to arrive in the order they were made, or a sign-out followed by a save
    /// leaves the application signed in on the next launch.
    /// </summary>
    [Fact]
    public async Task WritesAreQueuedInOrder_SoASignOutIsNotOvertaken()
    {
        MemorySecretStore secrets = new();
        SecretStoreSessionPersistence persistence = new(secrets, Key, NullLogger.Instance);

        await persistence.PrimeAsync(legacyFilePath: null, Ct);

        persistence.SaveSession(ASession("first"));
        persistence.SaveSession(ASession("second"));
        persistence.DestroySession();

        await persistence.DrainAsync();

        Assert.Null(await secrets.GetAsync(Key, Ct));
        Assert.Equal(2, secrets.Writes);
    }
}

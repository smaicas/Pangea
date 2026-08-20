using CdCSharp.Pangea.Supabase.Services;
using CdCSharp.Pangea.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Supabase.Tests;

/// <summary>
/// What happens before a single request is made.
/// </summary>
/// <remarks>
/// Nothing here reaches the network. What is worth pinning is the failure an unconfigured
/// application meets, because both halves of it otherwise surface as something else: an empty URL
/// becomes a connection error naming no host, and an empty key becomes a 401 that reads as a broken
/// backend.
/// </remarks>
public class SupabaseClientProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SupabaseClientProvider Arrange(SupabaseOptions options) =>
        new(Options.Create(options), new InMemoryStorageService(), NullLogger<SupabaseClientProvider>.Instance);

    [Fact]
    public void BeforeItIsInitialized_TheClientSaysSoRatherThanHandingOutAHalfBuiltOne()
    {
        SupabaseClientProvider provider = Arrange(new SupabaseOptions
        {
            Url = "https://probe.supabase.co",
            AnonKey = "anon"
        });

        Assert.False(provider.IsInitialized);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => provider.Client);

        Assert.Contains("InitializeAsync", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAUrl_TheFailureNamesTheOption()
    {
        SupabaseClientProvider provider = Arrange(new SupabaseOptions { AnonKey = "anon" });

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.InitializeAsync(Ct));

        Assert.Contains("SupabaseOptions.Url", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAnAnonKey_TheFailureNamesTheOptionAndWarnsOffTheOtherKey()
    {
        SupabaseClientProvider provider = Arrange(new SupabaseOptions { Url = "https://probe.supabase.co" });

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.InitializeAsync(Ct));

        Assert.Contains("SupabaseOptions.AnonKey", error.Message, StringComparison.Ordinal);
        Assert.Contains("service_role", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A failed attempt is not remembered: what it failed on was usually the network, which is back
    /// a moment later, and caching it would leave the application permanently offline until restart.
    /// </summary>
    [Fact]
    public async Task AFailedAttemptLeavesTheProviderReadyToTryAgain()
    {
        SupabaseClientProvider provider = Arrange(new SupabaseOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.InitializeAsync(Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.InitializeAsync(Ct));

        Assert.False(provider.IsInitialized);
    }
}

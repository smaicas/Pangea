using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Supabase.Abstractions;
using CdCSharp.Pangea.Supabase.Services;
using CdCSharp.Pangea.Supabase.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Supabase;

/// <summary>
/// A shared Postgres backend for the application: one client, a session that survives a restart,
/// and somewhere to put the writes made while it was out of reach.
/// </summary>
/// <remarks>
/// Registered by being present, like every Pangea feature. It configures nothing on its own -
/// <see cref="SupabaseOptions.Url"/> and <see cref="SupabaseOptions.AnonKey"/> have no sensible
/// default - and says so at startup rather than failing on the first query.
/// </remarks>
public sealed class SupabaseFeature : IPangeaFeature
{
    public string Name => "Supabase";

    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<SupabaseOptions>(_ => { });

        services.AddSingleton<ISupabaseClientProvider, SupabaseClientProvider>();
        services.AddSingleton<ISupabaseAuth, SupabaseAuth>();
        services.AddSingleton<IOutbox, FileOutbox>();

        // The connection is made while the splash is up rather than on the first screen's first
        // query, so a view model can ask who is signed in and get an answer.
        services.AddSingleton<IPangeaAsyncInitializer, SupabaseInitializer>();
    }
}

using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CdCSharp.Pangea.Data;

/// <summary>Registers a database with the application.</summary>
public static class PangeaDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/>, the factory that builds it, the maintenance
    /// operations for it and the startup initializer that brings its schema up to date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from the application's <c>Configure(IServiceCollection)</c>, beside everything else
    /// it registers for itself:
    /// </para>
    /// <code>
    /// services.AddPangeaDbContext&lt;AppDbContext&gt;(db =>
    /// {
    ///     db.UseSqlite();
    ///     db.Options.DatabaseFileName = "app.db";
    /// });
    /// </code>
    /// <para>
    /// Deliberately explicit rather than discovered: a context is a type the application wrote, the
    /// engine it talks to is a decision, and neither is something to work out by scanning
    /// assemblies at startup.
    /// </para>
    /// <para>
    /// <typeparamref name="TContext"/> needs a constructor taking
    /// <c>DbContextOptions&lt;TContext&gt;</c> - that is how the factory hands it the connection.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">No provider was chosen.</exception>
    public static IServiceCollection AddPangeaDbContext<TContext>(
        this IServiceCollection services, Action<PangeaDbBuilder> configure) where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        PangeaDbBuilder builder = new(services, typeof(TContext));
        configure(builder);

        IPangeaDbProvider provider = builder.Provider ?? throw new InvalidOperationException(
            $"No database provider was chosen for '{typeof(TContext).Name}'. The engines ship separately so that " +
            "an application carries only the driver it uses: install CdCSharp.Pangea.Data.Sqlite and call " +
            "db.UseSqlite() inside AddPangeaDbContext.");

        PangeaDbOptions options = builder.Options;

        // Registering the same context twice has almost no symptom - the second set of services is
        // mostly swallowed by TryAdd - except for a second startup initializer migrating the same
        // database again. Two databases means two context types.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(PangeaDbRuntime<TContext>)))
        {
            throw new InvalidOperationException(
                $"'{typeof(TContext).Name}' has already been registered with AddPangeaDbContext. Register a " +
                "second context type to talk to a second database, or configure this one in a single call.");
        }

        services.TryAddSingleton<IDatabaseLocator, StorageDatabaseLocator>();
        services.TryAddSingleton(serviceProvider => new PangeaDbRuntime<TContext>(
            options, provider, serviceProvider.GetRequiredService<IDatabaseLocator>()));

        void ConfigureContext(IServiceProvider serviceProvider, DbContextOptionsBuilder contextOptions)
        {
            PangeaDbRuntime<TContext> runtime = serviceProvider.GetRequiredService<PangeaDbRuntime<TContext>>();

            provider.Configure(contextOptions, runtime.ConnectionString, options);

            contextOptions.EnableSensitiveDataLogging(options.SensitiveDataLogging);
            contextOptions.EnableDetailedErrors(options.DetailedErrors);

            // EF logs through whatever the application configured for everything else. Without
            // this it builds a logger factory of its own and the queries go nowhere.
            if (serviceProvider.GetService<ILoggerFactory>() is { } loggerFactory)
            {
                contextOptions.UseLoggerFactory(loggerFactory);
            }
        }

        int beforeEntityFramework = services.Count;

        if (options.UsePooling)
        {
            services.AddPooledDbContextFactory<TContext>(ConfigureContext, options.MaxPoolSize);
        }
        else
        {
            services.AddDbContextFactory<TContext>(ConfigureContext);
        }

        // EF registers the context itself alongside the factory, scoped. A Pangea application has no
        // scopes, so injecting it would resolve from the root container and hand a view model a
        // context that lives as long as the process - the exact thing IPangeaDbContext exists to
        // prevent. Taking the registration away turns that into "no service for AppDbContext" at
        // startup instead of a growing change tracker nobody looks at.
        //
        // Only what the call above added: a registration the application made for itself is a
        // decision, and this is not the place to overrule it.
        for (int index = services.Count - 1; index >= beforeEntityFramework; index--)
        {
            if (services[index].ServiceType == typeof(TContext)) services.RemoveAt(index);
        }

        services.TryAddSingleton<IPangeaDbContext<TContext>, PangeaDbContextAccessor<TContext>>();
        services.TryAddSingleton<IDatabaseMaintenance<TContext>, DatabaseMaintenance<TContext>>();

        // Not TryAdd: initializers are a collection, and this one is this context's.
        services.AddSingleton<IPangeaAsyncInitializer, DatabaseInitializer<TContext>>();

        return services;
    }

    /// <summary>
    /// Registers data <typeparamref name="TContext"/> needs before the application can use it. Runs
    /// at startup, after the schema is up to date.
    /// </summary>
    public static IServiceCollection AddDataSeeder<TContext, TSeeder>(this IServiceCollection services)
        where TContext : DbContext
        where TSeeder : class, IDataSeeder<TContext>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDataSeeder<TContext>, TSeeder>();
        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace CdCSharp.Pangea.Data.Abstractions;

/// <summary>
/// How a view model reaches the database.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="DbContext"/> is a unit of work, not a service: it is not thread-safe, and it
/// remembers every entity it has loaded. Both are fine for a web request that lives for
/// milliseconds and wrong for a desktop application that runs for a day - a context injected into a
/// view model and kept would grow without bound and serve values that changed hours ago.
/// </para>
/// <para>
/// So nothing here hands out a context that outlives the call. Each method builds one from the
/// pooled factory, uses it, and disposes it.
/// </para>
/// </remarks>
public interface IPangeaDbContext<TContext> where TContext : DbContext
{
    /// <summary>
    /// A context of your own, to dispose when you are done with it. For work the methods below do
    /// not cover - a transaction spanning several steps, a bulk load.
    /// </summary>
    TContext Create();

    /// <summary>
    /// Runs a query. The context tracks nothing: what comes back is data for the UI, and tracking
    /// it would keep every row alive behind a context that is about to be disposed anyway.
    /// </summary>
    Task<TResult> ReadAsync<TResult>(
        Func<TContext, CancellationToken, Task<TResult>> read, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a change and saves it. Returns the number of rows written by
    /// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// Held to one at a time when the provider says writes have to be
    /// (<see cref="IPangeaDbProvider.SerializesWrites"/>), which is what keeps a SQLite
    /// application off <c>database is locked</c> the first time two screens save at once.
    /// </remarks>
    Task<int> WriteAsync(
        Func<TContext, CancellationToken, Task> write, CancellationToken cancellationToken = default);

    /// <summary>Runs a change, saves it, and returns what the change worked out.</summary>
    Task<TResult> WriteAsync<TResult>(
        Func<TContext, CancellationToken, Task<TResult>> write, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a query and hands back a collection ready to bind to.
    /// </summary>
    /// <remarks>
    /// Built on the UI thread. An <see cref="ObservableCollection{T}"/> filled on a background
    /// thread and then bound raises its first change notification from the wrong thread, which
    /// Avalonia reports somewhere else entirely.
    /// </remarks>
    Task<ObservableCollection<TItem>> ToObservableAsync<TItem>(
        Func<TContext, IQueryable<TItem>> query, CancellationToken cancellationToken = default);
}

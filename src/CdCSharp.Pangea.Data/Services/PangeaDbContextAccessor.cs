using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace CdCSharp.Pangea.Data.Services;

/// <summary>
/// The implementation of <see cref="IPangeaDbContext{TContext}"/>: one context per operation, from
/// the pooled factory, disposed before the call returns.
/// </summary>
internal sealed class PangeaDbContextAccessor<TContext> : IPangeaDbContext<TContext> where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly PangeaDbRuntime<TContext> _runtime;
    private readonly IUIDispatcher? _dispatcher;

    /// <remarks>
    /// The dispatcher is optional: the data feature works in a console or a test with no UI thread
    /// to marshal onto, and only <see cref="ToObservableAsync"/> ever needs one.
    /// </remarks>
    public PangeaDbContextAccessor(
        IDbContextFactory<TContext> factory,
        PangeaDbRuntime<TContext> runtime,
        IUIDispatcher? dispatcher = null)
    {
        _factory = factory;
        _runtime = runtime;
        _dispatcher = dispatcher;
    }

    public TContext Create() => _factory.CreateDbContext();

    public async Task<TResult> ReadAsync<TResult>(
        Func<TContext, CancellationToken, Task<TResult>> read, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        await using TContext context = await _factory.CreateDbContextAsync(cancellationToken);

        // The context is gone by the time the caller has the result, so tracking would only cost
        // the snapshot it takes of every row on the way out.
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        return await read(context, cancellationToken);
    }

    public Task<int> WriteAsync(
        Func<TContext, CancellationToken, Task> write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);

        return WriteCoreAsync(async (context, token) =>
        {
            await write(context, token);
            return await context.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public Task<TResult> WriteAsync<TResult>(
        Func<TContext, CancellationToken, Task<TResult>> write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);

        return WriteCoreAsync(async (context, token) =>
        {
            TResult result = await write(context, token);
            await context.SaveChangesAsync(token);
            return result;
        }, cancellationToken);
    }

    public async Task<ObservableCollection<TItem>> ToObservableAsync<TItem>(
        Func<TContext, IQueryable<TItem>> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<TItem> items = await ReadAsync(
            (context, token) => query(context).ToListAsync(token), cancellationToken);

        if (_dispatcher is null || _dispatcher.CheckAccess()) return new ObservableCollection<TItem>(items);

        return await _dispatcher.InvokeAsync(() => Task.FromResult(new ObservableCollection<TItem>(items)));
    }

    /// <summary>
    /// Where the write lock is taken, when the provider says writes have to be taken one at a time.
    /// </summary>
    private async Task<TResult> WriteCoreAsync<TResult>(
        Func<TContext, CancellationToken, Task<TResult>> write, CancellationToken cancellationToken)
    {
        SemaphoreSlim? gate = _runtime.WriteLock;

        if (gate is not null) await gate.WaitAsync(cancellationToken);

        try
        {
            await using TContext context = await _factory.CreateDbContextAsync(cancellationToken);
            return await write(context, cancellationToken);
        }
        finally
        {
            gate?.Release();
        }
    }
}

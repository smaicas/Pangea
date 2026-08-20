namespace CdCSharp.Pangea.Supabase.Abstractions;

/// <summary>
/// The writes made while the backend was out of reach, kept until they land.
/// </summary>
/// <remarks>
/// <para>
/// A phone loses its connection constantly and the user does not stop using the application when it
/// does. Without somewhere to put a write, the choice is between refusing it - which is what makes
/// an application feel broken on a train - and dropping it, which is worse.
/// </para>
/// <para>
/// The queue is deliberately untyped: what a pending write means is the application's business, and
/// a toolkit that tried to know would only be able to replay the operations it had been taught. It
/// stores what the application wrote and hands it back in the order it arrived.
/// </para>
/// <para>
/// Every member is safe to call from any thread. Writes are serialised, so two callers enqueueing
/// at once cannot lose one another's entry.
/// </para>
/// </remarks>
public interface IOutbox
{
    /// <summary>What is waiting, oldest first.</summary>
    Task<IReadOnlyList<OutboxEntry>> PendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a write to the back of the queue.</summary>
    /// <returns>The stored entry, with the id and timestamp it was given.</returns>
    Task<OutboxEntry> EnqueueAsync(string kind, string payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays what is waiting, oldest first, and removes each entry the handler accepted.
    /// </summary>
    /// <param name="handler">
    /// Performs one write. Returning <see langword="true"/> drops the entry; returning
    /// <see langword="false"/>, or throwing, leaves it queued with one more attempt recorded and
    /// stops the drain - the order writes were made in is usually the order they have to be applied
    /// in, so continuing past a failure would apply a later write on top of a missing earlier one.
    /// </param>
    /// <param name="cancellationToken">Stops the drain; what is left stays queued.</param>
    /// <returns>How many entries were accepted.</returns>
    Task<int> DrainAsync(
        Func<OutboxEntry, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken = default);

    /// <summary>Drops an entry without replaying it.</summary>
    /// <remarks>
    /// For a write the application has decided will never succeed - one the user has since undone,
    /// or that failed for a reason retrying cannot fix.
    /// </remarks>
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Empties the queue.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>One queued write.</summary>
/// <param name="Id">Given by the outbox, stable across restarts.</param>
/// <param name="Kind">What the application calls this write, so it knows how to replay it.</param>
/// <param name="Payload">Whatever the application needs to replay it, as it chose to serialise it.</param>
/// <param name="QueuedAt">When it was made, which is not when it will be applied.</param>
/// <param name="Attempts">How many times replaying it has been tried and has not succeeded.</param>
public sealed record OutboxEntry(
    string Id, string Kind, string Payload, DateTimeOffset QueuedAt, int Attempts);

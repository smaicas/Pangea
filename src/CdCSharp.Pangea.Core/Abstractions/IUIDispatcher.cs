namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>
/// Marshals work onto the UI thread.
/// </summary>
/// <remarks>
/// Implementations must let exceptions thrown by the supplied delegate propagate to the caller.
/// Swallowing them turns a broken handler into a silently dead one, which is far harder to
/// diagnose than a crash: whoever schedules the work is responsible for handling failure.
/// </remarks>
public interface IUIDispatcher
{
    /// <summary>True when the calling thread is the UI thread.</summary>
    bool CheckAccess();

    /// <summary>Queues <paramref name="action"/> on the UI thread and returns immediately.</summary>
    void Post(Action action);

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread and blocks until it completes.
    /// Runs it inline when already on the UI thread.
    /// </summary>
    void Invoke(Action action);
}

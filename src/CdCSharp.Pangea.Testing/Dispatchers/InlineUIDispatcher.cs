using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Testing.Dispatchers;

/// <summary>
/// Runs everything where it was called, and counts how it got there.
/// </summary>
/// <remarks>
/// The dispatcher to reach for when a test is about what a view model does, not about which thread
/// did it: no UI, no message loop, and a command's body has finished by the time
/// <c>Execute</c> returns. What it records is how work asked to reach the UI thread, which is the
/// part a command actually decides.
/// <para>
/// Use <see cref="PumpingUIDispatcher"/> instead when the question is whether a call waited.
/// </para>
/// </remarks>
public sealed class InlineUIDispatcher : IUIDispatcher
{
    /// <summary>Whether the caller is considered to be on the UI thread. True by default.</summary>
    public bool IsOnUIThread { get; set; } = true;

    public int InvokeCount { get; private set; }

    public int PostCount { get; private set; }

    /// <summary>
    /// Work queued while <see cref="AutoFlushPosts"/> is off, so a test can watch it accumulate.
    /// </summary>
    public List<Action> PendingPosts { get; } = [];

    /// <summary>Whether <see cref="Post"/> runs its work immediately. On by default.</summary>
    public bool AutoFlushPosts { get; set; } = true;

    public bool CheckAccess() => IsOnUIThread;

    public void Post(Action action)
    {
        PostCount++;

        if (AutoFlushPosts)
        {
            action();
        }
        else
        {
            PendingPosts.Add(action);
        }
    }

    public void Invoke(Action action)
    {
        InvokeCount++;
        action();
    }

    public T Invoke<T>(Func<T> callback)
    {
        InvokeCount++;
        return callback();
    }

    public Task InvokeAsync(Action action)
    {
        InvokeCount++;
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> callback)
    {
        InvokeCount++;
        return callback();
    }

    /// <summary>Runs everything queued by <see cref="Post"/> so far.</summary>
    public void FlushPosts()
    {
        List<Action> queued = new(PendingPosts);
        PendingPosts.Clear();

        foreach (Action action in queued)
        {
            action();
        }
    }
}

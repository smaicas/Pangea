using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Core.Tests.Infrastructure;

/// <summary>
/// Stands in for the Avalonia dispatcher so command threading can be asserted without a UI.
/// It records how work reached the "UI thread" instead of which OS thread ran it, which is the
/// part the command actually decides.
/// </summary>
internal sealed class FakeUIDispatcher : IUIDispatcher
{
    /// <summary>Whether the caller is considered to be on the UI thread.</summary>
    public bool IsOnUIThread { get; set; } = true;

    public int InvokeCount { get; private set; }

    public int PostCount { get; private set; }

    /// <summary>Queued work when <see cref="AutoFlushPosts"/> is off, so coalescing can be observed.</summary>
    public List<Action> PendingPosts { get; } = new();

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

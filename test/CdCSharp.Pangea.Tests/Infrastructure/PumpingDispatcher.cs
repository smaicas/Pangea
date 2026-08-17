using CdCSharp.Pangea.Core.Abstractions;
using System.Collections.Concurrent;

namespace CdCSharp.Pangea.Tests.Infrastructure;

/// <summary>
/// A dispatcher owned by the thread that constructs it, which runs queued work only when told to.
/// </summary>
/// <remarks>
/// Standing in for Avalonia's dispatcher is what makes "this call waited for the UI thread"
/// observable at all. Avalonia's own headless dispatcher has thread affinity on Windows and none on
/// Linux, so a test built on it asserts the platform rather than the window manager - and passes on
/// one and fails on the other.
/// </remarks>
internal sealed class PumpingDispatcher : IUIDispatcher
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private readonly ConcurrentQueue<Action> _queued = new();

    /// <summary>Work that was marshalled rather than run where it was called.</summary>
    public int MarshalledCount { get; private set; }

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _ownerThreadId;

    public void Post(Action action)
    {
        MarshalledCount++;
        _queued.Enqueue(action);
    }

    public void Invoke(Action action) => RunAndWait(action);

    public T Invoke<T>(Func<T> callback)
    {
        T result = default!;
        RunAndWait(() => result = callback());
        return result;
    }

    /// <summary>Runs <paramref name="action"/> on the owning thread and returns once it has.</summary>
    /// <remarks>
    /// A single-candidate method on purpose. Routing this through the public <c>Invoke</c> pair
    /// invites a silent infinite recursion: where a lambda body is an expression that produces a
    /// value - <c>() =&gt; result = callback()</c> is one - C# prefers the delegate that returns a
    /// value, so the call binds to <c>Invoke&lt;T&gt;</c> rather than the <c>Action</c> overload
    /// and the method calls itself. Writing the body as a block avoids it, which makes the fix a
    /// property of how the lambda happens to be written; having no overload to mis-bind to does
    /// not.
    /// </remarks>
    private void RunAndWait(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        // Blocking is the contract: the caller may rely on the work having happened when this
        // returns, which is the whole difference this dispatcher exists to expose.
        MarshalledCount++;
        using ManualResetEventSlim done = new();

        _queued.Enqueue(() =>
        {
            try
            {
                action();
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();
    }

    public Task InvokeAsync(Action action)
    {
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        MarshalledCount++;
        TaskCompletionSource completion = new();

        _queued.Enqueue(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task;
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> callback)
    {
        if (CheckAccess()) return callback();

        MarshalledCount++;
        TaskCompletionSource<T> completion = new();

        _queued.Enqueue(async void () =>
        {
            try
            {
                completion.SetResult(await callback());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        return completion.Task;
    }

    /// <summary>Runs everything queued so far, on the calling thread.</summary>
    public void Drain()
    {
        while (_queued.TryDequeue(out Action? queued))
        {
            queued();
        }
    }

    /// <summary>Keeps running queued work until <paramref name="work"/> finishes.</summary>
    public void DrainUntil(Func<bool> work)
    {
        while (!work())
        {
            Drain();
            Thread.Sleep(1);
        }

        Drain();
    }
}

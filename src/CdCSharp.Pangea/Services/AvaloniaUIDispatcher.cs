using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Services;

/// <summary>
/// <see cref="IUIDispatcher"/> backed by Avalonia's dispatcher.
/// </summary>
/// <remarks>
/// Exceptions from the scheduled delegate are deliberately not caught: whoever scheduled the work
/// decides what to do about it, and swallowing them here would turn a failing body into a silent
/// no-op.
/// </remarks>
public class AvaloniaUIDispatcher : IUIDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }

    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Invoke(action);
        }
    }

    public T Invoke<T>(Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return CheckAccess() ? callback() : Dispatcher.UIThread.Invoke(callback);
    }

    public async Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    public async Task<T> InvokeAsync<T>(Func<Task<T>> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (CheckAccess())
        {
            return await callback();
        }

        return await Dispatcher.UIThread.InvokeAsync(callback);
    }
}

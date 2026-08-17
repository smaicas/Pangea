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
}

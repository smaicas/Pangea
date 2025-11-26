using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Services;

/// <summary>
/// Avalonia implementation of IUIDispatcher for thread-safe UI updates
/// </summary>
public class AvaloniaUIDispatcher : IUIDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        if (action == null) return;
        
        try
        {
            Dispatcher.UIThread.Post(action);
        }
        catch
        {
            // Ignore dispatcher errors to prevent crashes
        }
    }

    public void Invoke(Action action)
    {
        if (action == null) return;
        
        try
        {
            if (CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.UIThread.Invoke(action);
            }
        }
        catch
        {
            // Ignore dispatcher errors to prevent crashes
        }
    }
}
namespace PangeaOmniApp.Services;

/// <summary>The last greeting another application asked for, so the window can show it.</summary>
public sealed class GreetingLog
{
    private readonly object _gate = new();

    public string? LastCaller { get; private set; }

    public string? LastName { get; private set; }

    /// <summary>Raised on the thread that recorded it: the bus's, not the UI's.</summary>
    public event EventHandler? Recorded;

    public void Record(string caller, string name)
    {
        lock (_gate)
        {
            LastCaller = caller;
            LastName = name;
        }

        Recorded?.Invoke(this, EventArgs.Empty);
    }
}

using Microsoft.Extensions.Logging;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Work going on that must not be cut short: an export, a recording, a batch, an answer another
/// application waits for. While any runs, the application says no when a host asks it to close.
/// </summary>
/// <remarks>
/// <para>
/// Mark the work with <see cref="Begin"/> and dispose what it returns when the work ends, however it
/// ends:
/// </para>
/// <code>
/// using (activity.Begin("Exporting the video"))
/// {
///     await exporter.ExportAsync(project, cancellationToken);
/// }
/// </code>
/// <para>
/// Registered by <see cref="AppsFeature"/>, which also registers the <see cref="IAppCloseHandler"/>
/// that consults it, so marking the work is all there is to do. An application with more to decide
/// before closing - saving, or asking a server - still writes a close handler of its own.
/// </para>
/// </remarks>
public sealed class AppActivity
{
    private readonly object _gate = new();
    private readonly List<Entry> _running = [];

    /// <summary>Whether any work is going on.</summary>
    public bool IsBusy
    {
        get
        {
            lock (_gate) return _running.Count > 0;
        }
    }

    /// <summary>What is going on, in the order it started, as described to <see cref="Begin"/>.</summary>
    public IReadOnlyList<string> Running
    {
        get
        {
            lock (_gate) return _running.Select(entry => entry.Description).ToArray();
        }
    }

    /// <summary>Raised on the thread that started or ended a piece of work.</summary>
    public event EventHandler? Changed;

    /// <summary>Marks work as going on until the result is disposed; disposing twice ends it once.</summary>
    /// <param name="description">What the work is, for a log or a status line: <c>Exporting the video</c>.</param>
    public IDisposable Begin(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Entry entry = new(this, description);

        lock (_gate) _running.Add(entry);

        Changed?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    private void End(Entry entry)
    {
        bool removed;

        lock (_gate) removed = _running.Remove(entry);

        if (removed) Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Entry(AppActivity activity, string description) : IDisposable
    {
        public string Description { get; } = description;

        public void Dispose() => activity.End(this);
    }
}

/// <summary>Keeps the application open while <see cref="AppActivity"/> has work going on.</summary>
internal sealed class AppActivityCloseHandler(AppActivity activity, ILogger<AppActivityCloseHandler> logger) : IAppCloseHandler
{
    public Task<bool> OnCloseRequestedAsync(string reason, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> running = activity.Running;

        if (running.Count == 0) return Task.FromResult(true);

        logger.LogInformation("Asked to close ({Reason}) while this is going on: {Work}", reason, string.Join("; ", running));
        return Task.FromResult(false);
    }
}

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// What the application has going on, for the host to show and to stop: a program it started, a service it runs, a model it
/// loaded - and the work in progress marked with <see cref="AppActivity"/>, as tasks. The host reaches a stop through the
/// application, which stops the thing its own way: unloading a model, stopping a bot, cancelling a download.
/// </summary>
/// <remarks>
/// <code>
/// IAppResource model = resources.Add(AppResourceKind.Model, "GLM-4.7-Flash", "CUDA0 · 9 GB",
///     stop: cancellationToken => server.UnloadAsync(cancellationToken));
/// model.Detail = "CUDA0 · 9 GB · answering";
/// model.Dispose();   // when it is no longer there
/// </code>
/// <para>
/// Registered by <see cref="AppsFeature"/>; the bus sends the list to a host that wants it each time it changes. Once a stop
/// has run, the resource leaves the list: disposing it afterwards is harmless.
/// </para>
/// </remarks>
public sealed class AppResources
{
    private readonly object _gate = new();
    private readonly List<Resource> _resources = [];
    private readonly AppActivity _activity;
    private long _next;

    public AppResources(AppActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        _activity = activity;
        _activity.Changed += (_, _) => Raise();
    }

    /// <summary>Everything going on: the resources added, then the work in progress.</summary>
    public IReadOnlyList<AppResourceInfo> Current
    {
        get
        {
            List<AppResourceInfo> current;

            lock (_gate) current = [.. _resources.Select(resource => resource.Describe())];

            current.AddRange(_activity.Describe());
            return current;
        }
    }

    /// <summary>Raised on the thread that added, changed or removed something.</summary>
    public event EventHandler? Changed;

    /// <summary>Lists something going on until the result is disposed.</summary>
    /// <param name="kind">What kind of thing it is: a process, a service, a model, a task.</param>
    /// <param name="title">What it is, for a person: <c>GLM-4.7-Flash</c>, <c>Bot Bender</c>.</param>
    /// <param name="detail">How it is doing, in a line; it can change later through <see cref="IAppResource.Detail"/>.</param>
    /// <param name="stop">Stops it when the person asks from the host; null when it cannot be stopped from there.</param>
    /// <param name="processId">The process it is, or runs in, when it is one of the application's own.</param>
    public IAppResource Add(AppResourceKind kind, string title, string? detail = null, Func<CancellationToken, Task>? stop = null, int? processId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Resource resource = new(this, $"res-{Interlocked.Increment(ref _next)}", kind, title, detail, processId, DateTimeOffset.Now, stop);

        lock (_gate) _resources.Add(resource);

        Raise();
        return resource;
    }

    /// <summary>Stops a resource or a piece of work by its <see cref="AppResourceInfo.Id"/>, as the host asks.</summary>
    /// <returns>False when there is no such thing, or it cannot be stopped.</returns>
    public async Task<bool> StopAsync(string id, CancellationToken cancellationToken = default)
    {
        Resource? resource;

        lock (_gate) resource = _resources.FirstOrDefault(candidate => candidate.Id == id);

        if (resource is null) return await _activity.StopAsync(id, cancellationToken).ConfigureAwait(false);

        if (resource.Stop is not { } stop) return false;

        await stop(cancellationToken).ConfigureAwait(false);
        resource.Dispose();
        return true;
    }

    private void Remove(Resource resource)
    {
        bool removed;

        lock (_gate) removed = _resources.Remove(resource);

        if (removed) Raise();
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed class Resource(
        AppResources owner,
        string id,
        AppResourceKind kind,
        string title,
        string? detail,
        int? processId,
        DateTimeOffset since,
        Func<CancellationToken, Task>? stop) : IAppResource
    {
        private string _title = title;
        private string? _detail = detail;
        private int? _processId = processId;

        public string Id { get; } = id;

        public Func<CancellationToken, Task>? Stop { get; } = stop;

        public string Title
        {
            get => _title;
            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                if (_title == value) return;

                _title = value;
                owner.Raise();
            }
        }

        public string? Detail
        {
            get => _detail;
            set
            {
                if (_detail == value) return;

                _detail = value;
                owner.Raise();
            }
        }

        public int? ProcessId
        {
            get => _processId;
            set
            {
                if (_processId == value) return;

                _processId = value;
                owner.Raise();
            }
        }

        public AppResourceInfo Describe() => new(Id, kind, _title)
        {
            Detail = _detail,
            ProcessId = _processId,
            Since = since,
            CanStop = Stop is not null
        };

        public void Dispose() => owner.Remove(this);
    }
}

/// <summary>Something listed in <see cref="AppResources"/>: change what it says as it changes, and dispose it when it is gone.</summary>
public interface IAppResource : IDisposable
{
    string Id { get; }

    string Title { get; set; }

    string? Detail { get; set; }

    int? ProcessId { get; set; }
}

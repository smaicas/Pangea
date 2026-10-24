namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Something another application asked the person to do on screen - edit an image, pick a song,
/// sign a form - waiting for them to finish or give up.
/// </summary>
/// <remarks>
/// Derive one per kind of request, carrying what the screen needs to show it, and run it through
/// <see cref="AppInteractions"/>. The screen ends it with <see cref="AppInteraction{TResult}.Complete"/>
/// or <see cref="Cancel"/>.
/// </remarks>
public abstract class AppInteraction
{
    protected AppInteraction(string requester)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requester);
        Requester = requester;
    }

    /// <summary>Who asked, as the person knows them: an application's name, or the caller's id when it gave none.</summary>
    public string Requester { get; }

    /// <summary>Whether the person has yet to finish it.</summary>
    public abstract bool IsOpen { get; }

    /// <summary>Gives up: the application that asked receives the cancelled answer. Does nothing once ended.</summary>
    public abstract void Cancel();
}

/// <summary>An <see cref="AppInteraction"/> that ends with a <typeparamref name="TResult"/> for the application that asked.</summary>
/// <typeparam name="TResult">The answer the request is waiting for.</typeparam>
public class AppInteraction<TResult> : AppInteraction
{
    private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TResult _cancelled;

    /// <param name="requester">Who asked.</param>
    /// <param name="cancelled">
    /// The answer the application that asked receives when the person gives up, the request is
    /// cancelled, or the screen fails to open - an answer, not an exception, because giving up is
    /// something the person chose.
    /// </param>
    public AppInteraction(string requester, TResult cancelled) : base(requester) => _cancelled = cancelled;

    /// <summary>The answer, once the person has finished or given up.</summary>
    public Task<TResult> Result => _completion.Task;

    public override bool IsOpen => !_completion.Task.IsCompleted;

    /// <summary>Ends it with the person's answer.</summary>
    /// <returns><see langword="false"/> when it had already ended.</returns>
    public bool Complete(TResult result) => _completion.TrySetResult(result);

    public override void Cancel() => _completion.TrySetResult(_cancelled);
}

/// <summary>
/// The requests from other applications that the person answers on screen, one at a time.
/// </summary>
/// <remarks>
/// <para>
/// A second request waits for the first to be finished or cancelled, rather than replacing what the
/// person is working on. While one is open the application refuses to close when a host asks, and
/// its main window is brought to the front when the screen for it opens.
/// </para>
/// <para>
/// Registered by <see cref="AppsFeature"/>. A request handler runs the interaction and returns its
/// answer; the screen watches <see cref="Current"/> to show it and ends it:
/// </para>
/// <code>
/// public async Task&lt;EditResult&gt; HandleAsync(EditImage request, AppRequestContext context, CancellationToken cancellationToken)
/// {
///     EditSession session = new(request.FilePath, context.CallerId);
///     return await interactions.RunAsync(session, () => ui.InvokeAsync(() => editor.Open(session)), cancellationToken);
/// }
/// </code>
/// </remarks>
public sealed class AppInteractions
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly AppActivity _activity;
    private readonly IServiceProvider _services;
    private int _waiting;

    /// <param name="activity">Where an open interaction is marked as work going on.</param>
    /// <param name="services">Where the main window and the UI thread are found, when there are any.</param>
    public AppInteractions(AppActivity activity, IServiceProvider services)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>The interaction on screen, or <see langword="null"/>.</summary>
    public AppInteraction? Current { get; private set; }

    /// <summary>How many more wait for their turn behind <see cref="Current"/>.</summary>
    public int Waiting => Math.Max(0, Volatile.Read(ref _waiting) - (Current is null ? 0 : 1));

    /// <summary>Raised on any thread when an interaction is queued, opens or ends.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Waits for the turn, opens the interaction, brings the main window forward and waits for the
    /// person's answer.
    /// </summary>
    /// <param name="interaction">What the person is asked to do.</param>
    /// <param name="open">
    /// Puts it on screen: navigates there, loads the file. <see cref="Current"/> is already
    /// <paramref name="interaction"/> when this runs. If it throws, the interaction is cancelled and
    /// the exception reaches the caller.
    /// </param>
    /// <param name="cancellationToken">
    /// The request's: cancelled while waiting for the turn, this throws; cancelled once open, the
    /// interaction is cancelled and the cancelled answer returned.
    /// </param>
    public async Task<TResult> RunAsync<TResult>(
        AppInteraction<TResult> interaction,
        Func<Task> open,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        ArgumentNullException.ThrowIfNull(open);

        Interlocked.Increment(ref _waiting);
        Changed?.Invoke(this, EventArgs.Empty);

        try
        {
            await _turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Decrement(ref _waiting);
            Changed?.Invoke(this, EventArgs.Empty);
            throw;
        }

        try
        {
            using (_activity.Begin($"{interaction.Requester} is waiting for an answer"))
            {
                Current = interaction;
                Changed?.Invoke(this, EventArgs.Empty);

                await open().ConfigureAwait(false);
                MainWindowFront.Bring(_services);

                await using (cancellationToken.Register(interaction.Cancel))
                {
                    return await interaction.Result.ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Ends it whatever happened, so nothing awaiting the result is left waiting for ever.
            interaction.Cancel();

            Current = null;
            Interlocked.Decrement(ref _waiting);
            _turn.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Cancels the interaction on screen, if any: the person gave up on it.</summary>
    public void CancelCurrent() => Current?.Cancel();
}

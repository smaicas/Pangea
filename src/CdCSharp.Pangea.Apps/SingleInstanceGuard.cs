using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipes;
using System.Text;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// One running copy of an application per user session, and a way for a second copy to reach the first.
/// </summary>
/// <remarks>
/// <para>
/// A named mutex says whether a copy is running; a named pipe, owned by that copy, is how another
/// one asks it to come to the front or to close. Both are named after the application id, so the
/// installed copy, one started by a host and one started from an IDE all find each other.
/// </para>
/// <para>
/// A copy that dies without releasing the mutex leaves it abandoned, which the next one reads as
/// free: a crash must not lock the application out until the user logs off.
/// </para>
/// </remarks>
internal sealed class SingleInstanceGuard : IDisposable
{
    internal const string ActivateCommand = "activate";
    internal const string ReplaceCommand = "replace";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stopping = new();

    private Mutex? _mutex;
    private bool _owned;

    public SingleInstanceGuard(string appId, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        string user = Sanitize(Environment.UserName);
        _mutexName = $@"Local\pangea-app-{Sanitize(appId)}";
        _pipeName = $"pangea-app-{Sanitize(appId)}-{user}";
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Whether this process holds the lock.</summary>
    public bool IsOwner => _owned;

    /// <summary>Takes the lock if no other copy holds it.</summary>
    public bool TryAcquire(TimeSpan wait)
    {
        _mutex ??= new Mutex(initiallyOwned: false, _mutexName);

        try
        {
            _owned = _mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            // The previous copy died holding it; it is ours now.
            _owned = true;
        }

        return _owned;
    }

    /// <summary>Sends <paramref name="command"/> to the running copy and returns its answer, or null when it could not be reached.</summary>
    public async Task<string?> SendAsync(string command, TimeSpan timeout)
    {
        try
        {
            using CancellationTokenSource cancellation = new(timeout);
            await using NamedPipeClientStream pipe = new(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            await pipe.ConnectAsync(cancellation.Token).ConfigureAwait(false);

            using StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using StreamReader reader = new(pipe, Encoding.UTF8, leaveOpen: true);

            await writer.WriteLineAsync(command.AsMemory(), cancellation.Token).ConfigureAwait(false);
            return await reader.ReadLineAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "The running copy did not answer '{Command}'", command);
            return null;
        }
    }

    /// <summary>
    /// Listens for other copies. <paramref name="onCommand"/> answers each command; its result is
    /// sent back as the answer, and only then is <paramref name="answered"/> told - so a copy that
    /// agrees to hand over closes after saying so, not while saying so.
    /// </summary>
    public void Listen(Func<string, Task<string>> onCommand, Action<string, string>? answered = null)
    {
        ArgumentNullException.ThrowIfNull(onCommand);

        if (!_owned) throw new InvalidOperationException("Only the copy that holds the lock listens.");

        _ = Task.Run(() => ListenAsync(onCommand, answered, _stopping.Token));
    }

    public void Dispose()
    {
        _stopping.Cancel();

        if (_owned)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Released from another thread than the one that took it; the OS releases it on exit.
            }
        }

        _mutex?.Dispose();
        _stopping.Dispose();
    }

    private async Task ListenAsync(Func<string, Task<string>> onCommand, Action<string, string>? answered, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeServerStream pipe = new(
                    _pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await pipe.WaitForConnectionAsync(stopping).ConfigureAwait(false);

                using StreamReader reader = new(pipe, Encoding.UTF8, leaveOpen: true);
                using StreamWriter writer = new(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

                string? command = await reader.ReadLineAsync(stopping).ConfigureAwait(false);

                if (command is null) continue;

                string answer = await onCommand(command).ConfigureAwait(false);
                await writer.WriteLineAsync(answer.AsMemory(), stopping).ConfigureAwait(false);

                answered?.Invoke(command, answer);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "A message from another copy of the application could not be read");
            }
        }
    }

    private static string Sanitize(string value)
    {
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.ToString();
    }
}

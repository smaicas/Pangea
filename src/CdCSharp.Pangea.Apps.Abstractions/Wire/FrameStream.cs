using System.Buffers.Binary;
using System.Text.Json;

namespace CdCSharp.Pangea.Apps.Wire;

/// <summary>
/// Reads and writes <see cref="Frame"/>s on a stream: a four-byte little-endian length, then that
/// many bytes of UTF-8 JSON.
/// </summary>
/// <remarks>
/// Writes are serialised with a lock, so any number of threads can send at once; reads are meant
/// for one loop. A clean end of stream between frames reads as <see langword="null"/>; one in the
/// middle of a frame is an error.
/// </remarks>
public sealed class FrameStream(Stream stream) : IAsyncDisposable
{
    /// <summary>Largest frame accepted. A file belongs on disk with its path on the bus, not in a frame.</summary>
    public const int MaxFrameBytes = 16 * 1024 * 1024;

    private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task WriteAsync(Frame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(frame, WireJson.Options);

        if (body.Length > MaxFrameBytes)
        {
            throw new InvalidOperationException(
                $"A '{frame.Message ?? frame.Kind.ToString()}' frame is {body.Length / 1024 / 1024} MB, over the " +
                $"{MaxFrameBytes / 1024 / 1024} MB limit. Send a file path rather than the file.");
        }

        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <returns>The next frame, or <see langword="null"/> when the other side closed the stream.</returns>
    public async Task<Frame?> ReadAsync(CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[4];

        int read = await _stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);

        if (read == 0) return null;

        if (read < header.Length) throw new EndOfStreamException("The pipe closed in the middle of a frame header.");

        int length = BinaryPrimitives.ReadInt32LittleEndian(header);

        if (length <= 0 || length > MaxFrameBytes)
        {
            throw new InvalidDataException($"A frame announced {length} bytes, which is not a frame this protocol sends.");
        }

        byte[] body = new byte[length];
        await _stream.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize<Frame>(body, WireJson.Options)
            ?? throw new InvalidDataException("A frame was empty.");
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }
}

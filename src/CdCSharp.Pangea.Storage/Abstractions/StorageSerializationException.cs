namespace CdCSharp.Pangea.Storage.Abstractions;

/// <summary>
/// The data could not be turned into JSON, or the JSON could not be turned back into the data.
/// </summary>
/// <remarks>
/// <para>
/// Separate from the <see cref="IOException"/> family on purpose. "The file could not be written"
/// is a condition an application recovers from - the disk is full, the file is open, the folder is
/// gone - and the honest answer is usually to try again later. "This object cannot be serialized"
/// is a defect in the application: it will fail identically on every run, on every machine, for
/// every user, and no amount of retrying will help.
/// </para>
/// <para>
/// Catching them together is how an application ends up silently discarding everything it meant to
/// save, which is exactly what an offline queue does when its own contents stopped being
/// serializable.
/// </para>
/// </remarks>
public sealed class StorageSerializationException : Exception
{
    public StorageSerializationException() { }

    public StorageSerializationException(string message) : base(message) { }

    public StorageSerializationException(string message, Exception innerException)
        : base(message, innerException) { }

    public StorageSerializationException(string message, string filePath, Type dataType, Exception innerException)
        : base(message, innerException)
    {
        FilePath = filePath;
        DataType = dataType;
    }

    /// <summary>The file the data was going to, or coming from.</summary>
    public string? FilePath { get; }

    /// <summary>The type that could not be converted.</summary>
    public Type? DataType { get; }
}

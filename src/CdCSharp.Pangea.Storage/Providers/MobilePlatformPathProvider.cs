using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Providers;

/// <summary>
/// Where an application's files go on Android and iOS.
/// </summary>
/// <remarks>
/// <para>
/// Neither platform has a home directory the application may write to, and neither is Linux as far
/// as <see cref="OperatingSystem"/> is concerned - <c>OperatingSystem.IsLinux()</c> is false on
/// Android. Without this the storage feature fell through to portable mode and wrote beside the
/// assembly, which on a device is a read-only part of the installed package.
/// </para>
/// <para>
/// The two folders .NET maps are the distinction both platforms actually make:
/// <see cref="Environment.SpecialFolder.ApplicationData"/> is the application's own data, backed up
/// with the device - <c>files/</c> on Android, <c>Library/</c> on iOS - and
/// <see cref="Environment.SpecialFolder.LocalApplicationData"/> is <c>Library/Caches</c> on iOS,
/// which the system may reclaim under storage pressure. Nothing that cannot be rebuilt belongs in
/// the cache on either.
/// </para>
/// <para>
/// The sandbox is per-application already, so scoping by name buys nothing here - it is kept only
/// because every other provider does it, and a path that changes shape between platforms is one
/// more thing to remember when reading a bug report from a device.
/// </para>
/// </remarks>
public class MobilePlatformPathProvider : IPlatformPathProvider
{
    private readonly IOptions<StorageOptions> _options;

    public MobilePlatformPathProvider(IOptions<StorageOptions> options) => _options = options;

    public string GetApplicationDataPath()
    {
        StorageOptions options = _options.Value;

        return !string.IsNullOrEmpty(options.CustomDataPath)
            ? options.CustomDataPath
            : Scoped(Environment.SpecialFolder.ApplicationData);
    }

    /// <summary>
    /// What the user would recognise as theirs, kept apart from the application's own state.
    /// </summary>
    /// <remarks>
    /// Composed from the platform folder rather than from
    /// <see cref="GetApplicationDataPath"/>, so <c>CustomDataPath</c> redirects application data
    /// and nothing else - the same rule every other provider follows.
    /// </remarks>
    public string GetUserDataPath() =>
        Path.Combine(Scoped(Environment.SpecialFolder.ApplicationData), "Documents");

    public string GetTempPath() =>
        Path.Combine(Path.GetTempPath(), _options.Value.ApplicationName);

    public string GetCachePath() =>
        Path.Combine(Scoped(Environment.SpecialFolder.LocalApplicationData), "Cache");

    /// <summary>
    /// The platform folder for this application, and somewhere writable when the platform hands
    /// back nothing.
    /// </summary>
    /// <remarks>
    /// <c>GetFolderPath</c> returns an empty string rather than throwing when it has no answer, and
    /// an empty base would silently turn every path below it into a relative one - written to
    /// whatever the working directory happens to be. The temporary directory is at least writable,
    /// and at least says where it went.
    /// </remarks>
    private string Scoped(Environment.SpecialFolder folder)
    {
        string path = Environment.GetFolderPath(folder);

        return Path.Combine(
            !string.IsNullOrEmpty(path) ? path : Path.GetTempPath(),
            _options.Value.ApplicationName);
    }
}

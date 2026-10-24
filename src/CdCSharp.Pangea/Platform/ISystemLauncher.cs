namespace CdCSharp.Pangea.Platform;

/// <summary>
/// Hands something to the operating system: a file to the application that opens it, a folder or a
/// file to the file manager, a link to the browser.
/// </summary>
/// <remarks>
/// Every method answers whether the system took it. None throws for a path that does not exist or a
/// platform that cannot do it - a button that does nothing is the worst outcome, and the answer is
/// there to say so instead.
/// </remarks>
public interface ISystemLauncher
{
    /// <summary>Opens a file with the application the system associates with it, or a folder in the file manager.</summary>
    /// <returns><see langword="false"/> when the path does not exist or the system would not open it.</returns>
    Task<bool> OpenAsync(string path);

    /// <summary>
    /// Shows a file in the file manager, selected where the platform can do that - Windows, macOS,
    /// and Linux file managers that follow the freedesktop interface - and its folder elsewhere. A
    /// folder is opened.
    /// </summary>
    /// <returns><see langword="false"/> when the path does not exist or the platform has no file manager to show it in.</returns>
    Task<bool> ShowInFolderAsync(string path);

    /// <summary>Opens a web address, or another URI, with whatever the system associates with its scheme.</summary>
    /// <returns><see langword="false"/> when the system would not open it.</returns>
    Task<bool> OpenUriAsync(Uri uri);
}

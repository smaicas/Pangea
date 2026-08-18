namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>
/// A splash window that reports what startup is doing.
/// </summary>
/// <remarks>
/// Optional: any window type will do as a splash, and one that does not implement this simply shows
/// whatever it was built with. Implementing it is how a custom splash gets the running
/// initializer's name, and how it is told when one failed.
/// <para>
/// Both members are called on the UI thread.
/// </para>
/// </remarks>
public interface IPangeaSplashView
{
    /// <summary>The initializer that is now running.</summary>
    void ReportStatus(string status);

    /// <summary>
    /// Startup failed. The window stays open afterwards: it is the only thing on screen, and
    /// closing it would leave the user with an application that vanished for no stated reason.
    /// </summary>
    void ReportFailure(string message);
}

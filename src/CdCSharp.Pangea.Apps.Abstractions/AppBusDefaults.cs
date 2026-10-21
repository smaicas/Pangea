using System.Text;

namespace CdCSharp.Pangea.Apps;

/// <summary>Where applications and the host find each other.</summary>
/// <remarks>
/// <para>
/// A host listens on a named pipe whose name is derived from the user, so an application started
/// from an IDE finds the host the user already has open with nothing passed to it. A host that
/// starts an application also sets <see cref="PipeVariable"/>, which wins, and
/// <see cref="HostVariable"/>, which says the application was started on its behalf.
/// </para>
/// <para>
/// The pipe only accepts connections from the same user: the bus is a way for one person's
/// applications to talk, not a network service.
/// </para>
/// </remarks>
public static class AppBusDefaults
{
    /// <summary>Environment variable a host sets to the pipe it listens on.</summary>
    public const string PipeVariable = "PANGEA_APPS_PIPE";

    /// <summary>Environment variable a host sets to its own name when it starts an application.</summary>
    public const string HostVariable = "PANGEA_APPS_HOST";

    /// <summary>The wire protocol spoken. A host refuses an application with a newer major one.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>The pipe to use: <see cref="PipeVariable"/> when set, <see cref="DefaultPipeName"/> otherwise.</summary>
    public static string PipeName =>
        Environment.GetEnvironmentVariable(PipeVariable) is { Length: > 0 } name ? name : DefaultPipeName;

    /// <summary>The pipe a host listens on when nothing says otherwise: one per user.</summary>
    public static string DefaultPipeName => "pangea-apps-" + Sanitize(Environment.UserName);

    /// <summary>The host that started this process, or null when it was started some other way.</summary>
    public static string? LaunchingHost =>
        Environment.GetEnvironmentVariable(HostVariable) is { Length: > 0 } host ? host : null;

    private static string Sanitize(string value)
    {
        StringBuilder builder = new(value.Length);

        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.Length == 0 ? "user" : builder.ToString();
    }
}

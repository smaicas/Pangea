using System.Collections.Concurrent;
using System.Reflection;

namespace CdCSharp.Pangea.Apps;

/// <summary>Reads the id a message type travels under.</summary>
public static class AppMessages
{
    private static readonly ConcurrentDictionary<Type, string> Ids = new();

    /// <summary>The id <typeparamref name="TMessage"/> is sent as.</summary>
    /// <exception cref="InvalidOperationException">The type carries no <see cref="AppMessageAttribute"/>.</exception>
    public static string IdOf<TMessage>() => IdOf(typeof(TMessage));

    /// <summary>The id <paramref name="messageType"/> is sent as.</summary>
    /// <exception cref="InvalidOperationException">The type carries no <see cref="AppMessageAttribute"/>.</exception>
    public static string IdOf(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return Ids.GetOrAdd(messageType, static type =>
            type.GetCustomAttribute<AppMessageAttribute>()?.Id is { Length: > 0 } id
                ? id
                : throw new InvalidOperationException(
                    $"'{type.FullName}' is sent over the bus but has no [AppMessage(\"app.area.verb\")]. " +
                    "The id is what the other application recognises it by."));
    }
}

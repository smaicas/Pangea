namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Names a message on the bus: the identifier that travels instead of the CLR type.
/// </summary>
/// <remarks>
/// <para>
/// Two applications agree on a message by this string, not by sharing an assembly version. The
/// sender serialises the record, the receiver deserialises it into its own copy of the type, and
/// as long as both say the same id the message arrives. That is what lets each application be
/// built, versioned and updated on its own.
/// </para>
/// <para>
/// By convention the id is <c>app.area.verb</c> - <c>yutuf.audio.download</c> - with the owning
/// application first, so ids from different applications cannot collide. Once published, an id
/// does not change: renaming it is removing one message and adding another.
/// </para>
/// </remarks>
/// <param name="id">The message id, lower case and dotted.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AppMessageAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}

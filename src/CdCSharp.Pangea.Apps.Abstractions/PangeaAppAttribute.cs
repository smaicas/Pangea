namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Identifies an assembly as a Pangea application: its manifest.
/// </summary>
/// <remarks>
/// <para>
/// Not written by hand. The <c>CdCSharp.Pangea.Apps</c> package emits it from the project's
/// <c>PangeaAppId</c>, <c>PangeaAppName</c>, <c>PangeaAppDescription</c> and <c>PangeaAppIcon</c>
/// properties.
/// </para>
/// <para>
/// A host reads it from the assembly's metadata without running any of its code, which is what
/// lets it list, update and start applications it knows nothing else about.
/// </para>
/// </remarks>
/// <param name="id">Stable, lower case: names the data folder, the single-instance lock and the messages it owns.</param>
/// <param name="name">What a person sees.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class PangeaAppAttribute(string id, string name) : Attribute
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public string? Description { get; set; }

    /// <summary>The icon file, relative to the application's folder.</summary>
    public string? Icon { get; set; }
}

/// <summary>
/// A message id the application answers. Emitted by the generator, once per handler, so a host
/// knows which application to start for a request without starting it to ask.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PangeaAppHandlesAttribute(string messageId) : Attribute
{
    public string MessageId { get; } = messageId;
}

namespace CdCSharp.Pangea.Binding.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class BindingAttribute : Attribute
{
    /// <summary>
    /// Generates a get-only property, without setter, change hook or notifications.
    /// </summary>
    public bool ReadOnly { get; set; } = false;

    /// <summary>
    /// Overrides the generated property name. Defaults to the field name without its
    /// leading underscore, capitalized.
    /// </summary>
    public string? PropertyName { get; set; }
}
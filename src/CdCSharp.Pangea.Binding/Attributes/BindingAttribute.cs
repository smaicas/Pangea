namespace CdCSharp.Pangea.Binding.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class BindingAttribute : Attribute
{
    public bool ReadOnly { get; set; } = false;
}
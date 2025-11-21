namespace MRubyCS.Wrapper.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MRubyClassAttribute : Attribute
{
    public string? Name { get; init; }
}

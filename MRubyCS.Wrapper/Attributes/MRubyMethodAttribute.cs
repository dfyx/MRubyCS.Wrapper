namespace MRubyCS.Wrapper.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MRubyMethodAttribute : Attribute
{
    public string? Name { get; init; }
}

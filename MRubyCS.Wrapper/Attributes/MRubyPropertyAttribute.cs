namespace MRubyCS.Wrapper.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class MRubyPropertyAttribute : Attribute
{
    public string? Name { get; init; }
    public bool QuestionMarkOnGetter { get; set; }
}

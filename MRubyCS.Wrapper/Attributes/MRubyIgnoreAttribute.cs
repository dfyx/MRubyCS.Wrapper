namespace MRubyCS.Wrapper.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, AllowMultiple = false)]
public sealed class MRubyIgnoreAttribute : Attribute
{
}

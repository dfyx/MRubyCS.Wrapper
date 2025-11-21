using System.Reflection;

namespace MRubyCS.Wrapper.Converters;

public interface IParameterConverter
{
    bool CanConvert(ParameterInfo parameter);

    bool TryConvert(MRubyWrapperHelper helper, MRubyValue input, ParameterInfo parameter, out object? output);
}

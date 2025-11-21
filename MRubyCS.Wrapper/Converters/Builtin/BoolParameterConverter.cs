
using System.Reflection;

namespace MRubyCS.Wrapper.Converters.Builtin;

public class BoolParameterConverter : IParameterConverter
{
    public bool CanConvert(ParameterInfo parameter)
    {
        return parameter.ParameterType == typeof(bool) || parameter.ParameterType == typeof(bool?);
    }

    public bool TryConvert(MRubyWrapperHelper helper, MRubyValue input, ParameterInfo parameter, out object? output)
    {
        if(!CanConvert(parameter))
        {
            output = null;
            return false;
        }

        if(input.IsNil)
        {
            output = null;
            return parameter.ParameterType == typeof(bool?);
        }

        if(input.IsTrue || input.IsFalse)
        {
            output = input.BoolValue;
            return true;
        }

        output = null;
        return false;
    }
}

using System.Reflection;

namespace MRubyCS.Wrapper.Converters.Builtin;

public class FloatParameterConverter : IParameterConverter
{
    public bool CanConvert(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        return type == typeof(float) || type == typeof(float?)
            || type == typeof(double) ||type == typeof(double?);
    }

    public bool TryConvert(MRubyWrapperHelper helper, MRubyValue input, ParameterInfo parameter, out object? output)
    {
        if (!CanConvert(parameter))
        {
            output = null;
            return false;
        }
        
        var type = parameter.ParameterType;
        if (input.IsNil)
        {
            output = null;
            return Nullable.GetUnderlyingType(type) != null;
        }

        if (input.IsFloat)
        {
            output = Convert.ChangeType(input.FloatValue, type);
            return true;
        }

        if (input.IsInteger)
        {
            output = Convert.ChangeType(input.IntegerValue, type);
            return true;
        }

        if (input.IsFixnum)
        {
            output = Convert.ChangeType(input.FixnumValue, type);
            return true;
        }
        
        output = null;
        return false;
    }
}
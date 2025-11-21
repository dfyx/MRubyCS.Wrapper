using System.Reflection;

namespace MRubyCS.Wrapper.Converters.Builtin;

public class IntegerParameterConverter : IParameterConverter
{
    public bool CanConvert(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        return type == typeof(int) || type == typeof(int?)
            || type == typeof(uint) ||type == typeof(uint?)
            || type == typeof(long) || type == typeof(long?)
            || type == typeof(ulong) ||type == typeof(ulong?)
            || type == typeof(short) || type == typeof(short?)
            || type == typeof(ushort) || type == typeof(ushort?)
            || type == typeof(byte) || type == typeof(byte?)
            || type == typeof(sbyte) || type == typeof(sbyte?);
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
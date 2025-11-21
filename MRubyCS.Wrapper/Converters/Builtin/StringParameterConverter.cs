using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MRubyCS.Wrapper.Converters.Builtin;

public class StringParameterConverter : IParameterConverter
{
    public bool CanConvert(ParameterInfo parameter)
    {
        return parameter.ParameterType == typeof(string);
    }

    public bool TryConvert(MRubyWrapperHelper helper, MRubyValue input, ParameterInfo parameter, out object? output)
    {
        if (!CanConvert(parameter))
        {
            output = null;
            return false;
        }

        if (input.IsNil)
        {
            output = null;
            return parameter.GetCustomAttribute<AllowNullAttribute>() != null;
        }

        if(input.IsSymbol)
        {
            output = helper.Mrb.NameOf(input.SymbolValue).ToString();
            return true;
        }

        if(input is { IsObject: true, Object: RString stringValue })
        {
            output = stringValue.ToString();
            return true;
        }

        output = null;
        return false;
    }
}

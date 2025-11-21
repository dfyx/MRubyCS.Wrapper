using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MRubyCS.Wrapper.Converters.Builtin;

public class RDataParameterConverter : IParameterConverter
{
    public bool CanConvert(ParameterInfo parameter)
    {
        return true;
    }

    public bool TryConvert(MRubyWrapperHelper helper, MRubyValue input, ParameterInfo parameter, out object? output)
    {
        var type = parameter.ParameterType;
        if (input.IsNil)
        {
            output = null;
            return Nullable.GetUnderlyingType(type) != null ||
                   parameter.GetCustomAttribute<AllowNullAttribute>() != null;
        }
        
        if (!input.IsObject)
        {
            var nativeObject = helper.GetNativeObject(input.As<RObject>());
            if (nativeObject != null && nativeObject.GetType().IsAssignableTo(type))
            {
                output = nativeObject;
                return true;
            }
        }
        
        output = null;
        return false;
    }
}
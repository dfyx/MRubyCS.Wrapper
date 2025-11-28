using MRubyCS.Wrapper.Attributes;
using MRubyCS.Wrapper.Converters;
using MRubyCS.Wrapper.Converters.Builtin;
using System.Reflection;
using System.Text;

namespace MRubyCS.Wrapper;

public class MRubyWrapperHelper
{
    private readonly Symbol m_nativeObjSymbol;
    private readonly Symbol m_initializeSymbol;
    
    public MRubyState Mrb { get; }

    private static readonly IParameterConverter[] BuiltinParameterConverters = [
        new BoolParameterConverter(),
        new IntegerParameterConverter(),
        new FloatParameterConverter(),
        new StringParameterConverter(),
        new RDataParameterConverter()
    ];

    public MRubyWrapperHelper(MRubyState mrb)
    {
        Mrb = mrb;
        m_nativeObjSymbol = mrb.Intern("__native_obj");
        m_initializeSymbol = mrb.Intern("initialize");
    }

    private readonly Dictionary<Type, RClass> m_classes = [];

    public object? GetNativeObject(RObject rubyObject)
    {
        var instanceVariable = Mrb.GetInstanceVariable(rubyObject, m_nativeObjSymbol);
        return instanceVariable.IsNil ? null : instanceVariable.As<RData>().Data;
    }

    public void SetNativeObject(RObject rubyObject, object nativeObject)
    {
        Mrb.SetInstanceVariable(rubyObject, m_nativeObjSymbol, new RData(nativeObject));
    }

    public void WrapClass<TClass>(Action<TClass>? constructorCallback = null)
    {
        var type = typeof(TClass);
        var classAttribute = type.GetCustomAttribute<MRubyClassAttribute>();

        var rubyClass = Mrb.DefineClass(Mrb.Intern(classAttribute?.Name ?? type.Name), Mrb.ObjectClass, options =>
        {
            WrapConstructors(options, constructorCallback);
            WrapMethods(options, type);
            WrapStaticMethods(options, type);
            WrapProperties(options, type);
            WrapStaticProperties(options, type);
        });

        m_classes.Add(type, rubyClass);
    }

    private void WrapConstructors<TClass>(ClassDefineOptions options, Action<TClass>? constructorCallback)
    {
        var constructors = typeof(TClass)
            .GetConstructors()
            .Where(c => c.GetCustomAttribute<MRubyIgnoreAttribute>() == null)
            .Select(GetMethodExecutionData);

        options.DefineMethod(m_initializeSymbol, (_, self) =>
        {
            var nativeObject = (TClass)Invoke(null, constructors, out var _)!;
            if (constructorCallback != null)
            {
                constructorCallback.Invoke(nativeObject);
            }

            SetNativeObject(self.As<RObject>(), nativeObject);
            return MRubyValue.Nil;
        });
    }

    private object? Invoke(object? @object, IEnumerable<MethodExecutionData> overloads, out MethodBase usedOverload)
    {
        foreach (var overload in overloads)
        {
            if (TryConverterParameters(overload, out var convertedValues))
            {
                usedOverload = overload.Method;
                if (usedOverload is ConstructorInfo constructor)
                {
                    return constructor.Invoke(convertedValues);
                }
                return usedOverload.Invoke(@object, convertedValues);
            }
        }
        
        throw new ArgumentException("No converters fit the given MRuby arguments");
    }

    private bool TryConverterParameters(MethodExecutionData overload, out object?[] convertedValues)
    {
        var parameterInfos = overload.Method.GetParameters();
        var parameterCount = parameterInfos.Length;
        convertedValues = new object?[parameterCount];
        
        // TODO: add support for optional parameters
        if (parameterCount != Mrb.GetArgumentCount())
        {
            return false;
        }
        
        for (var i = 0; i < parameterCount; i++)
        {
            var parameter = parameterInfos[i];
            var value = Mrb.GetArgumentAt(i);
            if (!TryConvertParameter(overload.ParameterConverters[i], value, parameter, out convertedValues[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryConvertParameter(IParameterConverter[] converters, MRubyValue value, ParameterInfo parameter, out object? convertedValue)
    {
        foreach (var converter in converters)
        {
            if (converter.TryConvert(this, value, parameter, out convertedValue))
            {
                return true;
            }
        }

        convertedValue = null;
        return false;
    }

    private MethodExecutionData GetMethodExecutionData(MethodBase method)
    {
        return new MethodExecutionData(
            method,
            method
                .GetParameters()
                .Select(p => BuiltinParameterConverters
                    .Where(c => c.CanConvert(p))
                    .ToArray() 
                    ?? throw new InvalidOperationException($"No converter found for parameter {p.Name} of method {method.Name}"))
                .ToArray());
    }

    private void WrapMethods(ClassDefineOptions options, Type type)
    {
        var methodGroups = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<MRubyIgnoreAttribute>() == null)
            .Select(GetMethodExecutionData)
            .GroupBy(d => GetRubyMethodName(d.Method));
        
        foreach (var group in methodGroups)
        {
            options.DefineMethod(Mrb.Intern(group.Key), (_, self) =>
            {
                var nativeObject = GetNativeObject(self.As<RObject>());
                var result = Invoke(nativeObject, group, out var method);
                return ((MethodInfo)method).ReturnType == typeof(void) ? MRubyValue.Nil : ConvertResult(result);
            });
        }
    }

    private void WrapStaticMethods(ClassDefineOptions options, Type type)
    {
        var methodGroups = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<MRubyIgnoreAttribute>() == null)
            .Select(GetMethodExecutionData)
            .GroupBy(d => GetRubyMethodName(d.Method));
        
        foreach (var group in methodGroups)
        {
            options.DefineClassMethod(Mrb.Intern(group.Key), (_, _) =>
            {
                var result = Invoke(null, group, out var method);
                return ((MethodInfo)method).ReturnType == typeof(void) ? MRubyValue.Nil : ConvertResult(result);
            });
        }
    }

    private void WrapProperties(ClassDefineOptions options, Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetCustomAttribute<MRubyIgnoreAttribute>() == null))
        {
            var propertyAttribute = property.GetCustomAttribute<MRubyPropertyAttribute>();
            var name = propertyAttribute?.Name ?? ToSnakeCase(property.Name);

            var getter = property.GetGetMethod();
            if (getter != null && getter.IsPublic)
            {
                var getterName = propertyAttribute?.QuestionMarkOnGetter == true ? name + "?" : name;
                var methodExecutionData =  GetMethodExecutionData(getter);
                options.DefineMethod(Mrb.Intern(getterName), (_, self) =>
                {
                    var nativeObject = GetNativeObject(self.As<RObject>());
                    if (!TryConverterParameters(methodExecutionData, out var parameters))
                    {
                        throw new ArgumentException();
                    }
                    var result = getter.Invoke(nativeObject, parameters);
                    return ConvertResult(result);
                });
            }

            var setter = property.GetSetMethod();
            if (setter != null && setter.IsPublic)
            {
                var methodExecutionData =  GetMethodExecutionData(setter);
                options.DefineMethod(Mrb.Intern(name + "="), (_, self) =>
                {
                    var nativeObject = GetNativeObject(self.As<RObject>());
                    if (!TryConverterParameters(methodExecutionData, out var parameters))
                    {
                        throw new ArgumentException();
                    }
                    setter.Invoke(nativeObject, parameters);
                    return MRubyValue.Nil;
                });
            }
        }
    }

    private void WrapStaticProperties(ClassDefineOptions options, Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.GetCustomAttribute<MRubyIgnoreAttribute>() == null))
        {
            var propertyAttribute = property.GetCustomAttribute<MRubyPropertyAttribute>();
            var name = propertyAttribute?.Name ?? ToSnakeCase(property.Name);

            var getter = property.GetGetMethod();
            if (getter != null && getter.IsPublic)
            {
                var getterName = propertyAttribute?.QuestionMarkOnGetter == true ? name + "?" : name;
                var methodExecutionData =  GetMethodExecutionData(getter);
                options.DefineClassMethod(Mrb.Intern(getterName), (_, _) =>
                {
                    if (!TryConverterParameters(methodExecutionData, out var parameters))
                    {
                        throw new ArgumentException();
                    }
                    var result = getter.Invoke(null, parameters);
                    return ConvertResult(result);
                });
            }

            var setter = property.GetSetMethod();
            if (setter != null && setter.IsPublic)
            {
                var methodExecutionData =  GetMethodExecutionData(setter);
                options.DefineClassMethod(Mrb.Intern(name + "="), (_, _) =>
                {
                    if (!TryConverterParameters(methodExecutionData, out var parameters))
                    {
                        throw new ArgumentException();
                    }
                    setter.Invoke(null, parameters);
                    return MRubyValue.Nil;
                });
            }
        }
    }

    private string GetRubyMethodName(MethodBase method)
    {
        var methodAttribute = method.GetCustomAttribute<Attributes.MRubyMethodAttribute>();
        return methodAttribute?.Name ?? ToSnakeCase(method.Name);
    }

    private MRubyValue ConvertResult(object? result)
    {
        if (result == null)
        {
            return MRubyValue.Nil;
        }

        if (result is bool boolValue)
        {
            return new MRubyValue(boolValue);
        }

        if (result is int intValue)
        {
            return new MRubyValue(intValue);
        }

        if (result is uint uintValue)
        {
            return new MRubyValue(uintValue);
        }

        if (result is long longValue)
        {
            return new MRubyValue(longValue);
        }

        if (result is ulong ulongValue)
        {
            return new MRubyValue(ulongValue);
        }

        if (result is float floatValue)
        {
            return new MRubyValue(floatValue);
        }

        if (result is double doubleValue)
        {
            return new MRubyValue(doubleValue);
        }

        if (result is string stringValue)
        {
            return Mrb.NewString(stringValue);
        }

        if (m_classes.TryGetValue(result.GetType(), out var rubyClass))
        {
            var rubyObject = new RObject(rubyClass);
            SetNativeObject(rubyObject, result);
            return rubyObject;
        }

        throw new ArgumentException($"No known converter for result type {result.GetType().FullName}");
    }

    private static string ToSnakeCase(string text)
    {
        if (text == null)
        {
            throw new ArgumentNullException(nameof(text));
        }
        if (text.Length < 2)
        {
            return text.ToLowerInvariant();
        }
        var sb = new StringBuilder();
        sb.Append(char.ToLowerInvariant(text[0]));
        for (int i = 1; i < text.Length; ++i)
        {
            char c = text[i];
            if (char.IsUpper(c))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private readonly struct MethodExecutionData(MethodBase method, IParameterConverter[][] parameterConverters)
    {
        public MethodBase Method { get; } = method;

        public IParameterConverter[][] ParameterConverters { get; } = parameterConverters;
    }
}

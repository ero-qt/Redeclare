using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Redeclare;

/// <summary>
///     Represents a typed view over an attribute's arguments, one accessor per argument list: the type arguments
///     of a generic attribute, the constructor arguments by parameter name or position, and the named property
///     or field arguments by name.
/// </summary>
/// <remarks>
///     <para>
///         The two lists are kept apart because an attribute may spell a parameter and a property the same way.
///         The caller always says which it means. A constructor argument is found by its parameter's exact name
///         whether it was passed positionally, explicitly named (<c>[Mark(level: Level.High)]</c>), or omitted
///         and left to the parameter's default. Names match exactly, as C# does.
///     </para>
///     <para>
///         Get one from <c>attribute.GetArguments()</c> after finding the attribute by its class symbol. The view
///         holds the <c>AttributeData</c>, so it is for the transform stage: read what is needed and carry the
///         values, not the view.
///     </para>
/// </remarks>
internal readonly struct AttributeArguments
{
    /// <summary>
    ///     Initializes a view over <paramref name="data"/>.
    /// </summary>
    public AttributeArguments(AttributeData data)
    {
        Data = data;
    }

    /// <summary>
    ///     Gets the attribute.
    /// </summary>
    public AttributeData Data { get; }

    /// <summary>
    ///     Gets the type arguments of a generic attribute, <c>[Tag&lt;int&gt;]</c>, as symbols. Empty otherwise.
    /// </summary>
    public ImmutableArray<ITypeSymbol> TypeArgumentSymbols
    {
        get
        {
            return Data.AttributeClass is { IsGenericType: true } attributeClass
                ? attributeClass.TypeArguments
                : [];
        }
    }

    /// <summary>
    ///     Gets the type arguments of a generic attribute as type references. Empty otherwise.
    /// </summary>
    public EquatableArray<TypeReference> TypeArguments
    {
        get
        {
            var symbols = TypeArgumentSymbols;
            var result = new TypeReference[symbols.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = SymbolReader.ReadTypeReference(symbols[i]);
            }

            return result;
        }
    }

    /// <summary>
    ///     The constructor argument for <paramref name="parameterName"/> as <typeparamref name="T"/>: a
    ///     primitive, a <see cref="string"/>, an enum (converted from the constant's underlying value), a
    ///     <see cref="TypeReference"/> from <c>typeof</c>, or the raw <c>ITypeSymbol</c>.
    /// </summary>
    public bool TryGetConstructorArgument<T>(string parameterName, out T value)
    {
        if (TryFindConstructor(parameterName, out var raw, out var type) && TryConvert(raw, type, out value))
        {
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    ///     The named argument for <paramref name="propertyName"/> as <typeparamref name="T"/>, converted as
    ///     constructor arguments are.
    /// </summary>
    public bool TryGetNamedArgument<T>(string propertyName, out T value)
    {
        if (TryFindNamed(propertyName, out var constant) && TryConvert(GetRawValue(constant), constant.Type, out value))
        {
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    ///     The constructor argument for <paramref name="parameterName"/>, or <paramref name="fallback"/>.
    /// </summary>
    public T ConstructorArgument<T>(string parameterName, T fallback = default!)
    {
        return TryGetConstructorArgument<T>(parameterName, out var value) ? value : fallback;
    }

    /// <summary>
    ///     The constructor argument at <paramref name="position"/>, or <paramref name="fallback"/>.
    /// </summary>
    public T ConstructorArgument<T>(int position, T fallback = default!)
    {
        var arguments = Data.ConstructorArguments;
        if (position < 0 || position >= arguments.Length)
        {
            return fallback;
        }

        var constant = arguments[position];

        return TryConvert<T>(GetRawValue(constant), constant.Type, out var value) ? value : fallback;
    }

    /// <summary>
    ///     The named argument for <paramref name="propertyName"/>, or <paramref name="fallback"/>.
    /// </summary>
    public T NamedArgument<T>(string propertyName, T fallback = default!)
    {
        return TryGetNamedArgument<T>(propertyName, out var value) ? value : fallback;
    }

    /// <summary>
    ///     A constructor argument that is an array, element by element. Empty when absent or not an array.
    /// </summary>
    public EquatableArray<T> ConstructorArray<T>(string parameterName)
    {
        return TryFindConstructorConstant(parameterName, out var constant) ? ConvertElements<T>(constant) : default;
    }

    /// <summary>
    ///     A named argument that is an array, element by element. Empty when absent or not an array.
    /// </summary>
    public EquatableArray<T> NamedArray<T>(string propertyName)
    {
        return TryFindNamed(propertyName, out var constant) ? ConvertElements<T>(constant) : default;
    }

    /// <summary>
    ///     The constructor argument as a C# expression, for passing through into generated code.
    /// </summary>
    public Snippet? ConstructorExpression(string parameterName)
    {
        if (TryFindConstructorConstant(parameterName, out var constant))
        {
            return SymbolReader.FormatConstant(constant);
        }

        return TryFindConstructor(parameterName, out var raw, out var type) && type is not null
            ? SymbolReader.FormatConstant(raw, type)
            : null;
    }

    /// <summary>
    ///     The named argument as a C# expression, for passing through into generated code.
    /// </summary>
    public Snippet? NamedExpression(string propertyName)
    {
        return TryFindNamed(propertyName, out var constant) ? SymbolReader.FormatConstant(constant) : null;
    }

    private static object? GetRawValue(TypedConstant constant)
    {
        return constant.Kind == TypedConstantKind.Array ? constant.Values : constant.Value;
    }

    private static EquatableArray<T> ConvertElements<T>(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Array || constant.IsNull)
        {
            return default;
        }

        List<T> values = [];
        foreach (var element in constant.Values)
        {
            if (TryConvert<T>(element.Value, element.Type, out var value))
            {
                values.Add(value);
            }
        }

        return values.ToEquatableArray();
    }

    private static bool TryConvert<T>(object? raw, ITypeSymbol? type, out T value)
    {
        switch (raw)
        {
            case T typed:
            {
                value = typed;
                return true;
            }
            case ITypeSymbol symbol when typeof(T) == typeof(TypeReference):
            {
                value = (T)(object)SymbolReader.ReadTypeReference(symbol);
                return true;
            }
            case null:
            {
                value = default!;
                return type is null || !type.IsValueType || default(T) is null;
            }
            case IConvertible convertible when typeof(T).IsEnum && convertible.GetTypeCode() is >= TypeCode.SByte and <= TypeCode.UInt64:
            {
                value = (T)Enum.ToObject(typeof(T), convertible);
                return true;
            }
            case IConvertible convertible when typeof(IConvertible).IsAssignableFrom(typeof(T)):
            {
                try
                {
                    value = (T)Convert.ChangeType(convertible, typeof(T), CultureInfo.InvariantCulture);
                    return true;
                }
                catch (InvalidCastException)
                {
                    value = default!;
                    return false;
                }
                catch (FormatException)
                {
                    value = default!;
                    return false;
                }
                catch (OverflowException)
                {
                    value = default!;
                    return false;
                }
            }
            default:
            {
                value = default!;
                return false;
            }
        }
    }

    private bool TryFindConstructorConstant(string parameterName, out TypedConstant constant)
    {
        if (Data.AttributeConstructor is { } constructor)
        {
            var parameters = constructor.Parameters;
            for (int i = 0; i < parameters.Length && i < Data.ConstructorArguments.Length; i++)
            {
                if (string.Equals(parameters[i].Name, parameterName, StringComparison.Ordinal))
                {
                    constant = Data.ConstructorArguments[i];
                    return true;
                }
            }
        }

        constant = default;
        return false;
    }

    /// <summary>
    ///     A constructor argument's value, falling back to the parameter's default when it was omitted.
    /// </summary>
    private bool TryFindConstructor(string parameterName, out object? value, out ITypeSymbol? type)
    {
        if (TryFindConstructorConstant(parameterName, out var constant))
        {
            value = GetRawValue(constant);
            type = constant.Type;
            return true;
        }

        if (Data.AttributeConstructor is { } constructor)
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (parameter.HasExplicitDefaultValue && string.Equals(parameter.Name, parameterName, StringComparison.Ordinal))
                {
                    value = parameter.ExplicitDefaultValue;
                    type = parameter.Type;
                    return true;
                }
            }
        }

        value = null;
        type = null;
        return false;
    }

    private bool TryFindNamed(string propertyName, out TypedConstant constant)
    {
        foreach (var named in Data.NamedArguments)
        {
            if (string.Equals(named.Key, propertyName, StringComparison.Ordinal))
            {
                constant = named.Value;
                return true;
            }
        }

        constant = default;
        return false;
    }
}

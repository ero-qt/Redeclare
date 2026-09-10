using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Redeclare;

internal static partial class SymbolReader
{
    private static readonly HashSet<string> _compilerAttributes = new(StringComparer.Ordinal)
    {
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
        "System.Runtime.CompilerServices.NullableAttribute",
        "System.Runtime.CompilerServices.NullableContextAttribute",
        "System.Runtime.CompilerServices.NullablePublicOnlyAttribute",
        "System.Runtime.CompilerServices.DynamicAttribute",
        "System.Runtime.CompilerServices.TupleElementNamesAttribute",
        "System.Runtime.CompilerServices.IsReadOnlyAttribute",
        "System.Runtime.CompilerServices.IsByRefLikeAttribute",
        "System.Runtime.CompilerServices.IsUnmanagedAttribute",
        "System.Runtime.CompilerServices.ExtensionAttribute",
        "System.Runtime.CompilerServices.NativeIntegerAttribute",
        "System.Runtime.CompilerServices.RequiredMemberAttribute",
        "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute",
        "System.Runtime.CompilerServices.ScopedRefAttribute",
        "System.Runtime.CompilerServices.RefSafetyRulesAttribute",
        "System.Runtime.CompilerServices.ParamCollectionAttribute",
        "System.Runtime.InteropServices.InAttribute",
        "System.Runtime.InteropServices.OutAttribute",
        "System.ParamArrayAttribute",
        "System.Reflection.DefaultMemberAttribute",
    };

    /// <summary>
    ///     Reads an attribute, or <see langword="null"/> for one the compiler emitted or that failed to bind.
    /// </summary>
    public static AttributeSpecification? ReadAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { TypeKind: not TypeKind.Error } attributeClass)
        {
            return null;
        }

        if (_compilerAttributes.Contains(attributeClass.ToDisplayString()))
        {
            return null;
        }

        List<Snippet> arguments = [];
        foreach (var argument in attribute.ConstructorArguments)
        {
            arguments.Add(Constant(argument));
        }

        foreach (var named in attribute.NamedArguments)
        {
            arguments.Add(Snippet.From($"{named.Key} = {Constant(named.Value)}"));
        }

        return new AttributeSpecification(
            Type: ReadTypeReference(attributeClass),
            Arguments: arguments.ToEquatableArray());
    }

    /// <summary>
    ///     Formats a constant as a C# expression: primitives as literals, enums by the member name that has the
    ///     value, <c>null</c> or <c>default</c> as the type demands. Types inside are holes.
    /// </summary>
    public static Snippet Constant(object? value, ITypeSymbol type)
    {
        if (value is null)
        {
            bool isNullable = type.NullableAnnotation == NullableAnnotation.Annotated
                || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

            return type.IsValueType && !isNullable ? "default" : "null";
        }

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumType)
        {
            var enumReference = ReadTypeReference(enumType) with { NullableAnnotation = NullableAnnotation.None };
            foreach (var member in enumType.GetMembers())
            {
                if (member is IFieldSymbol { HasConstantValue: true } field && Equals(field.ConstantValue, value))
                {
                    return Snippet.From($"{enumReference}.{field.Name}");
                }
            }

            return Snippet.From($"({enumReference}){Convert.ToString(value, CultureInfo.InvariantCulture)}");
        }

        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && type is INamedTypeSymbol nullable)
        {
            return Constant(value, nullable.TypeArguments[0]);
        }

        return Primitive(value);
    }

    /// <summary>
    ///     Formats an attribute argument as a C# expression, arrays and <c>typeof</c> included.
    /// </summary>
    public static Snippet Constant(TypedConstant constant)
    {
        return constant.Kind switch
        {
            TypedConstantKind.Error => "default",
            TypedConstantKind.Array when constant.IsNull => "null",
            TypedConstantKind.Array => ArrayConstant(constant),
            TypedConstantKind.Type => constant.Value is ITypeSymbol typeValue
                ? Snippet.From($"typeof({ReadTypeReference(typeValue)})")
                : "null",
            _ => constant.Type is { } type
                ? Constant(constant.Value, type)
                : constant.Value is { } value ? Primitive(value) : "default",
        };
    }

    /// <summary>
    ///     A primitive as a C# literal. <c>float</c> and <c>decimal</c> take their suffix, since the bare spelling
    ///     is a <c>double</c> literal.
    /// </summary>
    private static string Primitive(object value)
    {
        return value switch
        {
            float single => SymbolDisplay.FormatPrimitive(single, quoteStrings: false, useHexadecimalNumbers: false) + "f",
            decimal number => number.ToString(CultureInfo.InvariantCulture) + "m",
            _ => SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false) ?? "default",
        };
    }

    private static Snippet ArrayConstant(TypedConstant constant)
    {
        var values = new Snippet[constant.Values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Constant(constant.Values[i]);
        }

        var items = Snippet.Join(", ", values);

        return constant.Type is IArrayTypeSymbol array
            ? Snippet.From($"new {ReadTypeReference(array.ElementType)}[] {{ {items} }}")
            : Snippet.From($"new[] {{ {items} }}");
    }
}

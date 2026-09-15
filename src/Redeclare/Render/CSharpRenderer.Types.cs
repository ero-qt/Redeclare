using Microsoft.CodeAnalysis;
using System.Reflection.Metadata;
using System.Text;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    /// <summary>
    ///     Renders a type reference.
    /// </summary>
    public static string RenderType(TypeReference type, RenderOptions options)
    {
        return new StringBuilder().AppendType(type, options).ToString();
    }

    /// <summary>
    ///     Appends a type reference. Everything below this writes into the one builder, so a nested type or a
    ///     type argument costs no string of its own.
    /// </summary>
    public static StringBuilder AppendType(this StringBuilder text, TypeReference type, RenderOptions options)
    {
        return type switch
        {
            NamedTypeReference named => text.AppendNamedType(named, options),
            ArrayTypeReference array => text.AppendArray(array, options),
            PointerTypeReference pointer => text.AppendType(pointer.PointedAtType, options).Append('*'),
            FunctionPointerTypeReference functionPointer => text.AppendFunctionPointer(functionPointer, options),
            TupleTypeReference tuple => text.AppendTuple(tuple, options),
            TypeParameterReference parameter => text.AppendIdentifier(parameter.Name).AppendNullableSuffix(parameter, options),
            DynamicTypeReference dynamic => text.Append("dynamic").AppendNullableSuffix(dynamic, options),
            ErrorTypeReference error => text.Append(error.Text),
            _ => throw new RenderException($"Type reference of kind {type.GetType().Name} has no rendering."),
        };
    }

    /// <summary>
    ///     Appends the <c>?</c> a type reference carries, when the options and version allow one.
    /// </summary>
    private static StringBuilder AppendNullableSuffix(this StringBuilder text, TypeReference type, RenderOptions options)
    {
        if (type.NullableAnnotation != NullableAnnotation.Annotated)
        {
            return text;
        }

        return type.IsValueType || (options.NullableAnnotations && options.Allows(CSharpVersion.CSharp8))
            ? text.Append('?')
            : text;
    }

    private static StringBuilder AppendNamedType(this StringBuilder text, NamedTypeReference type, RenderOptions options)
    {
        // `void` has no other spelling.
        if (type.SpecialType == SpecialType.System_Void && type.ContainingType is null)
        {
            return text.Append("void");
        }

        if (WritesKeyword(type, options))
        {
            return text.Append(Keyword(type)).AppendNullableSuffix(type, options);
        }

        if (type.ContainingType is { } containing)
        {
            text.AppendNamedType(containing with { NullableAnnotation = NullableAnnotation.None }, options).Append('.');
        }
        else if (type.ContainingNamespace is { Length: > 0 } ns)
        {
            switch (options.Qualification)
            {
                case Qualification.Global:
                {
                    text.Append("global::").AppendQualifiedName(ns).Append('.');
                    break;
                }
                case Qualification.Full:
                {
                    text.AppendQualifiedName(ns).Append('.');
                    break;
                }
                case Qualification.Minimal:
                {
                    break;
                }
                default:
                {
                    throw new RenderException($"Unknown qualification {options.Qualification}.");
                }
            }
        }

        text.AppendIdentifier(type.Name);
        if (!type.TypeArguments.IsEmpty)
        {
            text.Append('<').AppendTypes(type.TypeArguments, options).Append('>');
        }
        else if (type.Arity > 0)
        {
            text.Append('<').Append(',', type.Arity - 1).Append('>');
        }

        return text.AppendNullableSuffix(type, options);
    }

    /// <summary>
    ///     Checks whether the type is written as its keyword. <c>nint</c> and <c>nuint</c> need C# 9. Below that, the
    ///     renderer writes <c>IntPtr</c> and <c>UIntPtr</c>.
    /// </summary>
    private static bool WritesKeyword(NamedTypeReference type, RenderOptions options)
    {
        return options.PredefinedTypeKeywords
            && Keyword(type) is not null
            && (!type.IsNativeIntegerType || options.Allows(CSharpVersion.CSharp9));
    }

    private static string? Keyword(NamedTypeReference type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Object => "object",
            SpecialType.System_String => "string",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Char => "char",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Void => "void",
            SpecialType.System_IntPtr when type.IsNativeIntegerType => "nint",
            SpecialType.System_UIntPtr when type.IsNativeIntegerType => "nuint",
            _ => null,
        };
    }

    private static StringBuilder AppendArray(this StringBuilder text, ArrayTypeReference type, RenderOptions options)
    {
        // An `int[,]` whose elements are `int[]` writes as `int[,][]`, and each `?` belongs to the layer inside it.
        TypeReference innermost = type;
        while (innermost is ArrayTypeReference layer)
        {
            innermost = layer.ElementType;
        }

        text.AppendType(innermost, options);

        var outer = type;
        while (true)
        {
            text.Append('[').Append(',', outer.Rank - 1).Append(']');
            if (outer.ElementType is ArrayTypeReference inner)
            {
                text.AppendNullableSuffix(inner, options);
                outer = inner;
            }
            else
            {
                return text.AppendNullableSuffix(type, options);
            }
        }
    }

    private static StringBuilder AppendTuple(this StringBuilder text, TupleTypeReference type, RenderOptions options)
    {
        text.Append('(');
        for (int i = 0; i < type.Elements.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            var element = type.Elements[i];
            text.AppendType(element.Type, options);
            if (element.Name is { } name)
            {
                text.Append(' ').AppendIdentifier(name);
            }
        }

        return text.Append(')').AppendNullableSuffix(type, options);
    }

    /// <summary>
    ///     Appends a function pointer type: <c>delegate*</c>, its calling convention, then its signature.
    /// </summary>
    private static StringBuilder AppendFunctionPointer(this StringBuilder text, FunctionPointerTypeReference type, RenderOptions options)
    {
        text.Append("delegate*");
        if (type.CallingConvention != SignatureCallingConvention.Default)
        {
            text.Append(" unmanaged");

            // Metadata names the convention through the enum, source through the types in the brackets.
            var namedConventions = type.UnmanagedCallingConventionTypes;
            if (!namedConventions.IsEmpty)
            {
                text.Append('[');
                for (int i = 0; i < namedConventions.Length; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    text.Append(namedConventions[i]);
                }

                text.Append(']');
            }
            else if (type.CallingConvention != SignatureCallingConvention.Unmanaged)
            {
                text.Append('[').Append(CallingConventionName(type.CallingConvention)).Append(']');
            }
        }

        text.Append('<');
        foreach (var parameter in type.Parameters)
        {
            text.Append(ParameterRefText(parameter.RefKind, "A function pointer parameter"))
                .AppendType(parameter.Type, options)
                .Append(", ");
        }

        return text.Append(RefText(type.RefKind, "A function pointer return"))
            .AppendType(type.ReturnType, options)
            .Append('>');
    }

    /// <summary>
    ///     Gets the name C# puts inside <c>unmanaged[...]</c> for a calling convention the metadata names by enum.
    /// </summary>
    private static string CallingConventionName(SignatureCallingConvention convention)
    {
        return convention switch
        {
            SignatureCallingConvention.CDecl => "Cdecl",
            SignatureCallingConvention.StdCall => "Stdcall",
            SignatureCallingConvention.ThisCall => "Thiscall",
            SignatureCallingConvention.FastCall => "Fastcall",
            _ => throw new RenderException($"Calling convention {convention} has no name C# can write."),
        };
    }
}

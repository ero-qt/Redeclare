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
        // `void` has no other spelling. The rest follow the preference.
        if (type.SpecialType == SpecialType.System_Void && type.ContainingType is null)
        {
            return text.Append("void");
        }

        // nint and nuint are C# 9. Below that, we need to use IntPtr and UIntPtr explicitly.
        if (options.PredefinedTypeKeywords && Keyword(type) is { } keyword && (!type.IsNativeIntegerType || options.Allows(CSharpVersion.CSharp9)))
        {
            return text.Append(keyword).AppendNullableSuffix(type, options);
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
        // An array of arrays is written outermost first: an int[,] whose elements are int[] is int[,][], not
        // int[][,]. The annotations shift by one against that order, so an array of int[]? is int[]?[] and a
        // nullable array of int[] is int[,][]?. Walk to the innermost element type, write it, then write each
        // rank with the annotation of the layer inside it, the outermost annotation last.
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
        Require(options, CSharpVersion.CSharp9, "a function pointer type", "A type reference");

        text.Append("delegate*");
        if (type.CallingConvention != SignatureCallingConvention.Default)
        {
            text.Append(" unmanaged");

            // A signature read from metadata names its convention through the enum. One read from source names it
            // through the types inside the brackets, which is what the compiler keeps for the named conventions.
            var names = type.UnmanagedCallingConventionTypes;
            if (!names.IsEmpty)
            {
                text.Append('[');
                for (int i = 0; i < names.Length; i++)
                {
                    if (i > 0)
                    {
                        text.Append(", ");
                    }

                    text.Append(names[i]);
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
            text.Append(ParameterRefText(parameter.RefKind, options, "A function pointer parameter"))
                .AppendType(parameter.Type, options)
                .Append(", ");
        }

        return text.Append(RefText(type.RefKind, options, "A function pointer return"))
            .AppendType(type.ReturnType, options)
            .Append('>');
    }

    /// <summary>
    ///     The name C# puts inside <c>unmanaged[...]</c> for a convention the metadata names by enum.
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

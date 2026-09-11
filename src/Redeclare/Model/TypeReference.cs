using Microsoft.CodeAnalysis;
using System;
using System.Globalization;
using System.Reflection.Metadata;

namespace Redeclare;

/// <summary>
///     Represents a reference to a type as it appears in a signature or a body, such as <c>List&lt;int&gt;</c>,
///     <c>string?</c>, <c>int[,]</c> or <c>T</c>.
/// </summary>
/// <remarks>
///     <para>
///         A reference holds the parts of a type rather than its text, so one value renders under any
///         <see cref="RenderOptions"/>: <c>global::System.Collections.Generic.List&lt;int&gt;</c>,
///         <c>System.Collections.Generic.List&lt;int&gt;</c> or <c>List&lt;int&gt;</c>.
///     </para>
///     <para>
///         Each kind of type has its own record and is built through its own constructor. A nullable type such as
///         <c>int?</c> is the reference to <c>Int32</c> with <see cref="NullableAnnotation.Annotated"/>.
///     </para>
/// </remarks>
/// <param name="NullableAnnotation">
///     The nullable annotation. <see cref="NullableAnnotation.Annotated"/> renders a trailing <c>?</c>.
/// </param>
internal abstract partial record TypeReference(
    NullableAnnotation NullableAnnotation = NullableAnnotation.None)
{
    /// <summary>
    ///     Gets a value indicating whether the type is a value type, which decides whether a trailing <c>?</c> is
    ///     <c>Nullable&lt;T&gt;</c> or an annotation.
    /// </summary>
    public abstract bool IsValueType { get; }

    /// <inheritdoc/>
    public sealed override string ToString()
    {
        return CSharpRenderer.RenderType(this, RenderOptions.Default);
    }
}

/// <summary>
///     Represents a reference to a class, struct, interface, enum or delegate by name, with its namespace,
///     containing type and type arguments.
/// </summary>
/// <param name="Name">The simple name, without namespace, containing type, arity suffix or type arguments.</param>
/// <param name="ContainingNamespace">
///     The namespace of a top-level type, or <see langword="null"/> for the global namespace.
/// </param>
/// <param name="ContainingType">
///     The containing type of a nested type. The outermost type's namespace is the one that renders.
/// </param>
/// <param name="TypeKind">Class, struct, interface, enum or delegate, which decides whether a <c>?</c> is an annotation.</param>
/// <param name="Arity">The number of type parameters the type declares. Zero for a non-generic type.</param>
/// <param name="TypeArguments">
///     The type arguments. Empty on an unbound generic type, which renders as <c>List&lt;&gt;</c>.
/// </param>
/// <param name="SpecialType">
///     The special type, when this is one of the types C# has a keyword for. The renderer writes the keyword
///     when <see cref="RenderOptions.PredefinedTypeKeywords"/> is on and the name otherwise.
/// </param>
/// <param name="IsNativeIntegerType">
///     Whether an <c>IntPtr</c> or <c>UIntPtr</c> is the native integer <c>nint</c> or <c>nuint</c>.
/// </param>
/// <param name="NullableAnnotation">The nullable annotation.</param>
internal sealed record NamedTypeReference(
    string Name,
    string? ContainingNamespace = null,
    NamedTypeReference? ContainingType = null,
    TypeKind TypeKind = TypeKind.Class,
    int Arity = 0,
    EquatableArray<TypeReference> TypeArguments = default,
    SpecialType SpecialType = SpecialType.None,
    bool IsNativeIntegerType = false,
    NullableAnnotation NullableAnnotation = NullableAnnotation.None)
    : TypeReference(NullableAnnotation)
{
    /// <inheritdoc/>
    public override bool IsValueType => TypeKind is TypeKind.Struct or TypeKind.Enum;

    /// <summary>
    ///     Gets a value indicating whether the type is generic but has no type arguments supplied.
    /// </summary>
    public bool IsUnboundGenericType => Arity > 0 && TypeArguments.IsEmpty;

    /// <summary>
    ///     Gets the metadata name: the simple name with its arity suffix, <c>List`1</c>.
    /// </summary>
    public string MetadataName => Arity > 0 ? Name + "`" + Arity.ToString(CultureInfo.InvariantCulture) : Name;

    /// <summary>
    ///     Returns a reference to this type constructed with the specified type arguments. A type with a known
    ///     <see cref="Arity"/> must receive that many.
    /// </summary>
    public NamedTypeReference Construct(params TypeReference[] typeArguments)
    {
        if (Arity > 0 && typeArguments.Length != Arity)
        {
            throw new ArgumentException(
                $"'{MetadataName}' takes {Arity} type arguments, not {typeArguments.Length}.", nameof(typeArguments));
        }

        return this with { TypeArguments = typeArguments, Arity = typeArguments.Length };
    }
}

/// <summary>
///     Represents a reference to an array type.
/// </summary>
/// <param name="ElementType">The element type.</param>
/// <param name="Rank">The rank. <c>int[,]</c> has rank two.</param>
/// <param name="NullableAnnotation">The nullable annotation of the array itself.</param>
internal sealed record ArrayTypeReference(
    TypeReference ElementType,
    int Rank = 1,
    NullableAnnotation NullableAnnotation = NullableAnnotation.None)
    : TypeReference(NullableAnnotation)
{
    /// <inheritdoc/>
    public override bool IsValueType => false;
}

/// <summary>
///     Represents a reference to a pointer type.
/// </summary>
/// <param name="PointedAtType">The type pointed at.</param>
internal sealed record PointerTypeReference(
    TypeReference PointedAtType)
    : TypeReference
{
    /// <inheritdoc/>
    public override bool IsValueType => true;
}

/// <summary>
///     Represents a reference to a function pointer type, such as <c>delegate* unmanaged[Cdecl]&lt;int, void&gt;</c>.
///     Needs C# 9.
/// </summary>
/// <remarks>
///     Every <paramref name="CallingConvention"/> other than <see cref="SignatureCallingConvention.Default"/>
///     renders as <c>unmanaged</c>, with the convention in brackets when there is one to name.
/// </remarks>
/// <param name="ReturnType">The return type.</param>
/// <param name="RefKind">How the return is passed back: by value, <c>ref</c>, or <c>ref readonly</c>.</param>
/// <param name="Parameters">The parameters, in order.</param>
/// <param name="CallingConvention">The calling convention.</param>
/// <param name="UnmanagedCallingConventionTypes">
///     The names inside <c>unmanaged[...]</c>, without their <c>CallConv</c> prefix, for a convention C# names
///     that way. Empty for a bare <c>unmanaged</c>.
/// </param>
internal sealed record FunctionPointerTypeReference(
    TypeReference ReturnType,
    RefKind RefKind = RefKind.None,
    EquatableArray<FunctionPointerParameter> Parameters = default,
    SignatureCallingConvention CallingConvention = SignatureCallingConvention.Default,
    EquatableArray<string> UnmanagedCallingConventionTypes = default)
    : TypeReference
{
    /// <inheritdoc/>
    public override bool IsValueType => true;
}

/// <summary>
///     Represents a parameter of a function pointer type: a type and how it is passed.
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="RefKind">
///     How the parameter is passed: by value, <c>ref</c>, <c>out</c>, <c>in</c>, or <c>ref readonly</c>.
/// </param>
internal sealed record FunctionPointerParameter(
    TypeReference Type,
    RefKind RefKind = RefKind.None);

/// <summary>
///     Represents a reference to a tuple type, such as <c>(int count, string)</c>.
/// </summary>
/// <param name="Elements">The elements, in order.</param>
/// <param name="NullableAnnotation">The nullable annotation.</param>
internal sealed record TupleTypeReference(
    EquatableArray<TupleElement> Elements,
    NullableAnnotation NullableAnnotation = NullableAnnotation.None)
    : TypeReference(NullableAnnotation)
{
    /// <inheritdoc/>
    public override bool IsValueType => true;
}

/// <summary>
///     Represents one element of a tuple type, with its optional name.
/// </summary>
/// <param name="Type">The element type.</param>
/// <param name="Name">The element name, or <see langword="null"/> for a positional element.</param>
internal sealed record TupleElement(
    TypeReference Type,
    string? Name = null);

/// <summary>
///     Represents a reference to a type parameter, such as <c>T</c>. It renders as its bare name under every
///     qualification.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="HasValueTypeConstraint">
///     Whether the parameter is constrained to value types, which makes <c>T?</c> a <c>Nullable&lt;T&gt;</c>
///     rather than an annotation.
/// </param>
/// <param name="NullableAnnotation">The nullable annotation.</param>
internal sealed record TypeParameterReference(
    string Name,
    bool HasValueTypeConstraint = false,
    NullableAnnotation NullableAnnotation = NullableAnnotation.None)
    : TypeReference(NullableAnnotation)
{
    /// <inheritdoc/>
    public override bool IsValueType => HasValueTypeConstraint;
}

/// <summary>
///     Represents a reference to <c>dynamic</c>.
/// </summary>
/// <param name="NullableAnnotation">The nullable annotation.</param>
internal sealed record DynamicTypeReference(
    NullableAnnotation NullableAnnotation = NullableAnnotation.None)
    : TypeReference(NullableAnnotation)
{
    /// <inheritdoc/>
    public override bool IsValueType => false;
}

/// <summary>
///     Represents a reference written out as given text, for a type the other kinds cannot express.
/// </summary>
/// <param name="Text">The text to write.</param>
internal sealed record ErrorTypeReference(
    string Text)
    : TypeReference
{
    /// <inheritdoc/>
    public override bool IsValueType => false;
}

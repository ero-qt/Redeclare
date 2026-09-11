using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     The types C# has a keyword for, so a generator that names one does not construct it by hand. Every other type
///     comes from a symbol, through <c>ToTypeReference()</c> or a reference built where the compilation is in reach.
/// </summary>
/// <remarks>
///     Each one renders as its keyword under <see cref="RenderOptions.PredefinedTypeKeywords"/>, and as its framework
///     name without it. <see cref="NativeInt"/> and <see cref="NativeUInt"/> render as <c>IntPtr</c> and
///     <c>UIntPtr</c> below C# 9, which is the same type in metadata.
/// </remarks>
internal abstract partial record TypeReference
{
    /// <summary>Gets <c>object</c>.</summary>
    public static NamedTypeReference Object { get; } = Predefined("Object", SpecialType.System_Object, TypeKind.Class);

    /// <summary>Gets <c>string</c>.</summary>
    public static NamedTypeReference String { get; } = Predefined("String", SpecialType.System_String, TypeKind.Class);

    /// <summary>Gets <c>bool</c>.</summary>
    public static NamedTypeReference Boolean { get; } = Predefined("Boolean", SpecialType.System_Boolean);

    /// <summary>Gets <c>char</c>.</summary>
    public static NamedTypeReference Char { get; } = Predefined("Char", SpecialType.System_Char);

    /// <summary>Gets <c>sbyte</c>.</summary>
    public static NamedTypeReference SByte { get; } = Predefined("SByte", SpecialType.System_SByte);

    /// <summary>Gets <c>byte</c>.</summary>
    public static NamedTypeReference Byte { get; } = Predefined("Byte", SpecialType.System_Byte);

    /// <summary>Gets <c>short</c>.</summary>
    public static NamedTypeReference Int16 { get; } = Predefined("Int16", SpecialType.System_Int16);

    /// <summary>Gets <c>ushort</c>.</summary>
    public static NamedTypeReference UInt16 { get; } = Predefined("UInt16", SpecialType.System_UInt16);

    /// <summary>Gets <c>int</c>.</summary>
    public static NamedTypeReference Int32 { get; } = Predefined("Int32", SpecialType.System_Int32);

    /// <summary>Gets <c>uint</c>.</summary>
    public static NamedTypeReference UInt32 { get; } = Predefined("UInt32", SpecialType.System_UInt32);

    /// <summary>Gets <c>long</c>.</summary>
    public static NamedTypeReference Int64 { get; } = Predefined("Int64", SpecialType.System_Int64);

    /// <summary>Gets <c>ulong</c>.</summary>
    public static NamedTypeReference UInt64 { get; } = Predefined("UInt64", SpecialType.System_UInt64);

    /// <summary>Gets <c>float</c>.</summary>
    public static NamedTypeReference Single { get; } = Predefined("Single", SpecialType.System_Single);

    /// <summary>Gets <c>double</c>.</summary>
    public static NamedTypeReference Double { get; } = Predefined("Double", SpecialType.System_Double);

    /// <summary>Gets <c>decimal</c>.</summary>
    public static NamedTypeReference Decimal { get; } = Predefined("Decimal", SpecialType.System_Decimal);

    /// <summary>Gets <c>void</c>.</summary>
    public static NamedTypeReference Void { get; } = Predefined("Void", SpecialType.System_Void);

    /// <summary>Gets <c>nint</c>.</summary>
    public static NamedTypeReference NativeInt { get; } =
        Predefined("IntPtr", SpecialType.System_IntPtr) with { IsNativeIntegerType = true };

    /// <summary>Gets <c>nuint</c>.</summary>
    public static NamedTypeReference NativeUInt { get; } =
        Predefined("UIntPtr", SpecialType.System_UIntPtr) with { IsNativeIntegerType = true };

    private static NamedTypeReference Predefined(string name, SpecialType specialType, TypeKind kind = TypeKind.Struct)
    {
        return new(Name: name, ContainingNamespace: "System", TypeKind: kind, SpecialType: specialType);
    }
}

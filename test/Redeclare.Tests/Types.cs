using Microsoft.CodeAnalysis;

namespace Redeclare.Tests;

/// <summary>
///     The type references the tests reach for, built the way the reader will build them from symbols.
/// </summary>
internal static class Types
{
    public static NamedTypeReference Void { get; } = Special("Void", SpecialType.System_Void, TypeKind.Struct);

    public static NamedTypeReference Object { get; } = Special("Object", SpecialType.System_Object, TypeKind.Class);

    public static NamedTypeReference String { get; } = Special("String", SpecialType.System_String, TypeKind.Class);

    public static NamedTypeReference Int32 { get; } = Special("Int32", SpecialType.System_Int32, TypeKind.Struct);

    public static NamedTypeReference Int64 { get; } = Special("Int64", SpecialType.System_Int64, TypeKind.Struct);

    public static NamedTypeReference StringBuilder { get; } = new(Name: "StringBuilder", ContainingNamespace: "System.Text");

    public static NamedTypeReference List { get; } = new(Name: "List", ContainingNamespace: "System.Collections.Generic", Arity: 1);

    private static NamedTypeReference Special(string name, SpecialType specialType, TypeKind kind)
    {
        return new(Name: name, ContainingNamespace: "System", TypeKind: kind, SpecialType: specialType);
    }
}

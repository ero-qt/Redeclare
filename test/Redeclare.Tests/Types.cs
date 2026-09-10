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

    public static NamedTypeReference Boolean { get; } = Special("Boolean", SpecialType.System_Boolean, TypeKind.Struct);

    public static NamedTypeReference Byte { get; } = Special("Byte", SpecialType.System_Byte, TypeKind.Struct);

    public static NamedTypeReference Int32 { get; } = Special("Int32", SpecialType.System_Int32, TypeKind.Struct);

    public static NamedTypeReference Int64 { get; } = Special("Int64", SpecialType.System_Int64, TypeKind.Struct);

    public static NamedTypeReference IDisposable { get; } = new(Name: "IDisposable", ContainingNamespace: "System", TypeKind: TypeKind.Interface);

    public static NamedTypeReference Action { get; } = new(Name: "Action", ContainingNamespace: "System", TypeKind: TypeKind.Delegate);

    public static NamedTypeReference EventHandler { get; } = new(Name: "EventHandler", ContainingNamespace: "System", TypeKind: TypeKind.Delegate);

    public static NamedTypeReference Func2 { get; } = new(Name: "Func", ContainingNamespace: "System", TypeKind: TypeKind.Delegate, Arity: 2);

    public static NamedTypeReference Console { get; } = new(Name: "Console", ContainingNamespace: "System");

    public static NamedTypeReference SerializableAttribute { get; } = new(Name: "SerializableAttribute", ContainingNamespace: "System");

    public static NamedTypeReference Stream { get; } = new(Name: "Stream", ContainingNamespace: "System.IO");

    public static NamedTypeReference StringBuilder { get; } = new(Name: "StringBuilder", ContainingNamespace: "System.Text");

    public static NamedTypeReference List { get; } = new(Name: "List", ContainingNamespace: "System.Collections.Generic", Arity: 1);

    public static TypeParameterReference T { get; } = new(Name: "T");

    public static NamedTypeReference Nullable(NamedTypeReference type)
    {
        return type with { NullableAnnotation = NullableAnnotation.Annotated };
    }

    private static NamedTypeReference Special(string name, SpecialType specialType, TypeKind kind)
    {
        return new(Name: name, ContainingNamespace: "System", TypeKind: kind, SpecialType: specialType);
    }
}

using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a class, struct, interface, enum, record, record struct or delegate.
/// </summary>
/// <remarks>
///     <para>
///         <paramref name="TypeKind"/> and <paramref name="IsRecord"/> together say what is declared: a record is
///         <see cref="TypeKind.Class"/> with <paramref name="IsRecord"/> set, and a delegate is
///         <see cref="TypeKind.Delegate"/> with <paramref name="ReturnType"/> and <paramref name="ParameterList"/>.
///     </para>
///     <para>
///         A new partial part of an existing type is this record with <see cref="Modifiers.Partial"/> added and
///         only what a second part may repeat: no attributes, no primary constructor list, no members that the
///         other part declares.
///     </para>
/// </remarks>
/// <param name="Name">The name, without type parameters.</param>
/// <param name="TypeKind">Class, struct, interface, enum or delegate. A record is a class or struct with <paramref name="IsRecord"/>.</param>
/// <param name="IsRecord">Whether this is a <c>record</c> or <c>record struct</c>. Needs C# 9, or 10 for a record struct.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="TypeParameters">The type parameters, constraints included.</param>
/// <param name="ParameterList">
///     The primary constructor parameter list, rendered as <c>Name(parameters)</c> when non-empty. Records need
///     C# 9, other types need C# 12. A delegate's parameters live here and need nothing.
/// </param>
/// <param name="BaseType">The base class, or <see langword="null"/> for <c>object</c> or none.</param>
/// <param name="Interfaces">The implemented interfaces, in declaration order.</param>
/// <param name="EnumUnderlyingType">The underlying type of an enum, when it is not <c>int</c>.</param>
/// <param name="ReturnType">A delegate's return type. Required when <paramref name="TypeKind"/> is a delegate, and refused otherwise.</param>
/// <param name="RefKind">How a delegate returns: by value, <c>ref</c>, or <c>ref readonly</c>.</param>
/// <param name="ContainingType">
///     The type this one is declared in, and only its shape: kind, name and type parameters. It says where this
///     declaration goes, and the renderer writes the chain out as partial parts around it. The outer type does
///     not hold this one, so a <c>with</c> on this one edits this one alone.
/// </param>
/// <param name="Members">The members, in render order. An enum holds only <see cref="EnumMemberDeclaration"/>.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
/// <param name="Overrides">Render options for this declaration and everything under it.</param>
internal sealed record TypeDeclaration(
    string Name,
    TypeKind TypeKind = TypeKind.Class,
    bool IsRecord = false,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    EquatableArray<TypeParameterDeclaration> TypeParameters = default,
    EquatableArray<ParameterDeclaration> ParameterList = default,
    TypeReference? BaseType = null,
    EquatableArray<TypeReference> Interfaces = default,
    TypeReference? EnumUnderlyingType = null,
    TypeReference? ReturnType = null,
    RefKind RefKind = RefKind.None,
    TypeDeclaration? ContainingType = null,
    EquatableArray<MemberDeclaration> Members = default,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default,
    RenderOverrides? Overrides = null)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers, Overrides)
{
    /// <summary>
    ///     Returns this type with members appended.
    /// </summary>
    public TypeDeclaration AddMembers(params MemberDeclaration[] members)
    {
        return this with { Members = Members.AddRange(members) };
    }
}

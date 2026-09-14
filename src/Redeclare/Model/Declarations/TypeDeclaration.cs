using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a class, struct, interface, enum, record or record struct. A delegate is a
///     <see cref="DelegateDeclaration"/>.
/// </summary>
/// <remarks>
///     <para>
///         <paramref name="TypeKind"/> and <paramref name="IsRecord"/> together say what is declared: a record is
///         <see cref="TypeKind.Class"/> with <paramref name="IsRecord"/> set.
///     </para>
///     <para>
///         A new partial part of an existing type is this record with <see cref="Modifiers.Partial"/> added and
///         only what a second part may repeat: no attributes, no primary constructor list, no members that the
///         other part declares.
///     </para>
/// </remarks>
/// <param name="Name">The name, without type parameters.</param>
/// <param name="TypeKind">Class, struct, interface or enum. A record is a class or struct with <paramref name="IsRecord"/>.</param>
/// <param name="IsRecord">Whether this is a <c>record</c> or <c>record struct</c>. Needs C# 9, or 10 for a record struct.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="TypeParameters">The type parameters, constraints included.</param>
/// <param name="ParameterList">
///     The primary constructor parameter list, rendered as <c>Name(parameters)</c> when non-empty. Records need
///     C# 9, other types need C# 12.
/// </param>
/// <param name="BaseType">The base class, or <see langword="null"/> for <c>object</c> or none.</param>
/// <param name="BaseArguments">
///     The arguments a primary constructor passes to the base class, the part inside the parentheses of
///     <c>: Base(x)</c>, or <see langword="null"/> for none. Needs <paramref name="BaseType"/>.
/// </param>
/// <param name="Interfaces">The implemented interfaces, in declaration order.</param>
/// <param name="EnumUnderlyingType">The underlying type of an enum, when it is not <c>int</c>.</param>
/// <param name="ContainingType">
///     The type this one is declared in, or <see langword="null"/> for a top-level type. A type rendered on its own
///     is written inside that chain, each outer type as given with this one as its sole member, so a read type
///     carries <c>partial</c> parts with nothing of their own. A type rendered as a member of another keeps this
///     as a fact and is written where the member list puts it.
/// </param>
/// <param name="Members">The members, in render order. An enum holds only <see cref="EnumMemberDeclaration"/>.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record TypeDeclaration(
    string Name,
    TypeKind TypeKind = TypeKind.Class,
    bool IsRecord = false,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    EquatableArray<TypeParameterDeclaration> TypeParameters = default,
    EquatableArray<ParameterDeclaration> ParameterList = default,
    TypeReference? BaseType = null,
    Snippet? BaseArguments = null,
    EquatableArray<TypeReference> Interfaces = default,
    TypeReference? EnumUnderlyingType = null,
    TypeDeclaration? ContainingType = null,
    EquatableArray<MemberDeclaration> Members = default,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers)
{
    /// <summary>
    ///     Returns this type with members appended.
    /// </summary>
    public TypeDeclaration AddMembers(params MemberDeclaration[] members)
    {
        return this with { Members = Members.AddRange(members) };
    }
}

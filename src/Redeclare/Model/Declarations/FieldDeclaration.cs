using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a field with one declarator.
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="Name">The name.</param>
/// <param name="RefKind">
///     Whether the field is a <c>ref</c> or <c>ref readonly</c> field, which only a <c>ref struct</c> may
///     declare. Needs C# 11.
/// </param>
/// <param name="FixedSize">
///     The element count of a fixed-size buffer, <c>fixed byte Data[16]</c>, or 0 for a field that is not one. The
///     <paramref name="Type"/> is the element type. Only an <c>unsafe</c> struct may declare one.
/// </param>
/// <param name="Initializer">The initializer, the part after <c>=</c>, or <see langword="null"/>.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record FieldDeclaration(
    TypeReference Type,
    string Name,
    RefKind RefKind = RefKind.None,
    int FixedSize = 0,
    Snippet? Initializer = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers);

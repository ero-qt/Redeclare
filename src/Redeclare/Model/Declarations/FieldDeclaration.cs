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
/// <param name="Initializer">The initializer, the part after <c>=</c>, or <see langword="null"/>.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
/// <param name="Overrides">Render options for this declaration and everything under it.</param>
internal sealed record FieldDeclaration(
    TypeReference Type,
    string Name,
    RefKind RefKind = RefKind.None,
    Snippet? Initializer = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default,
    RenderOverrides? Overrides = null)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers, Overrides);

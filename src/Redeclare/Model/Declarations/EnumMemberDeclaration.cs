using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a member of an enum. Accessibility and modifiers are ignored.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Value">The value, the part after <c>=</c>, or <see langword="null"/> to let the compiler count.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
/// <param name="Overrides">Render options for this declaration.</param>
internal sealed record EnumMemberDeclaration(
    string Name,
    Snippet? Value = null,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default,
    RenderOverrides? Overrides = null)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility.NotApplicable, Modifiers.None, Overrides);

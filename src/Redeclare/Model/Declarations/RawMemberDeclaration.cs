using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents verbatim member text, dedented and re-indented like a body, with holes filled. The escape
///     hatch for conversion operators, finalizers, and anything else without a typed declaration.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
/// <param name="Overrides">Render options for this declaration.</param>
internal sealed record RawMemberDeclaration(
    Snippet Text,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default,
    RenderOverrides? Overrides = null)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility.NotApplicable, Modifiers.None, Overrides);

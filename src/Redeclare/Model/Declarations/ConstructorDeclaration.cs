using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a constructor. Its name is the containing type's, supplied at render time.
/// </summary>
/// <remarks>
///     <paramref name="Body"/> is required because a constructor almost always has one. Pass
///     <see cref="Snippet.Empty"/> for an empty block. <see langword="null"/> renders <c>;</c>, which only an
///     <c>extern</c> constructor or a <c>partial</c> definition may have.
/// </remarks>
/// <param name="Body">The body, statements or one expression, or <see langword="null"/> for none.</param>
/// <param name="Parameters">The parameters.</param>
/// <param name="Initializer">The initializer without the colon, <c>base(x)</c> or <c>this()</c>, if any.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility. <see cref="Modifiers.Static"/> for a static constructor.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record ConstructorDeclaration(
    Snippet? Body,
    EquatableArray<ParameterDeclaration> Parameters = default,
    Snippet? Initializer = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers);

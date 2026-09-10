using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents an event, field-like or with accessors.
/// </summary>
/// <remarks>
///     Both accessors <see langword="null"/> is a field-like event, <c>event T Name;</c>. C# requires an event
///     that declares one accessor to declare both, so supplying only one is a <see cref="RenderException"/>.
/// </remarks>
/// <param name="Type">The delegate type.</param>
/// <param name="Name">The name.</param>
/// <param name="Adder">The <c>add</c> accessor, or <see langword="null"/> for a field-like event.</param>
/// <param name="Remover">The <c>remove</c> accessor, or <see langword="null"/> for a field-like event.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record EventDeclaration(
    TypeReference Type,
    string Name,
    AccessorDeclaration? Adder = null,
    AccessorDeclaration? Remover = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers);

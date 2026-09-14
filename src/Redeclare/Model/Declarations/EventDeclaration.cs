using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents an event, field-like or with accessors.
/// </summary>
/// <param name="Type">The delegate type.</param>
/// <param name="Name">The name.</param>
/// <param name="Accessors">The <c>add</c> and <c>remove</c> accessors, or <see langword="null"/> for a field-like event, <c>event T Name;</c>.</param>
/// <param name="ExplicitInterfaceSpecifier">
///     The interface for an explicit implementation, <c>event T IFoo.Name</c>, or <see langword="null"/>. C# requires
///     an explicit implementation to declare both accessors.
/// </param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record EventDeclaration(
    TypeReference Type,
    string Name,
    EventAccessors? Accessors = null,
    TypeReference? ExplicitInterfaceSpecifier = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers);

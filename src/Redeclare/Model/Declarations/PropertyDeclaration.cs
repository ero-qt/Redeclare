using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a property or indexer.
/// </summary>
/// <remarks>
///     Both accessors are required so that the shape is always spelled out. <see langword="null"/> is the
///     accessor's absence. A getter-only property whose getter is one expression collapses to
///     <c>Type Name =&gt; expression;</c> when the options' preference for properties allows. An indexer is a
///     property with <paramref name="Parameters"/>. Its <paramref name="Name"/> is ignored and it renders as
///     <c>this[...]</c>.
/// </remarks>
/// <param name="Type">The type.</param>
/// <param name="Name">The name. Ignored for an indexer.</param>
/// <param name="Getter">The getter, or <see langword="null"/> for none.</param>
/// <param name="Setter">The setter, or <see langword="null"/> for none.</param>
/// <param name="RefKind">How the value is passed back: by value, <c>ref</c>, or <c>ref readonly</c>. A ref property has no setter.</param>
/// <param name="Initializer">The initializer, the part after <c>=</c>, or <see langword="null"/>. Only an auto property may have one.</param>
/// <param name="Parameters">The indexer parameters. Non-empty makes this an indexer.</param>
/// <param name="ExplicitInterfaceSpecifier">The interface an explicit implementation names before the dot, if any.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record PropertyDeclaration(
    TypeReference Type,
    string Name,
    AccessorDeclaration? Getter,
    AccessorDeclaration? Setter,
    RefKind RefKind = RefKind.None,
    Snippet? Initializer = null,
    EquatableArray<ParameterDeclaration> Parameters = default,
    TypeReference? ExplicitInterfaceSpecifier = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers)
{
    /// <summary>
    ///     Gets a value indicating whether this is an indexer.
    /// </summary>
    public bool IsIndexer => !Parameters.IsEmpty;
}

using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents what every member declaration carries: documentation, attributes, accessibility, modifiers,
///     and render overrides for the subtree.
/// </summary>
/// <remarks>
///     Declarations are positional records: constructed with named arguments, changed with <c>with</c>,
///     rendered by the renderer. Every required part is a parameter without a default, so a declaration
///     that compiles is complete. <see langword="null"/> always means absence.
/// </remarks>
/// <param name="DocumentationComment">
///     The documentation comment as XML without the <c>///</c> prefixes, one line per line; what
///     <c>ISymbol.GetDocumentationCommentXml()</c> returns with its <c>&lt;member&gt;</c> wrapper removed.
/// </param>
/// <param name="Attributes">The attributes, each rendered on its own line.</param>
/// <param name="Accessibility">The accessibility. <see cref="Accessibility.NotApplicable"/> writes no modifier.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
internal abstract record MemberDeclaration(
    string? DocumentationComment,
    EquatableArray<AttributeSpecification> Attributes,
    Accessibility Accessibility,
    Modifiers Modifiers);

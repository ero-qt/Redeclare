using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a delegate: a signature under a type name, <c>delegate int Picker&lt;T&gt;(in T value);</c>.
/// </summary>
/// <param name="ReturnType">The return type. A reference to <c>System.Void</c> for none.</param>
/// <param name="Name">The name, without type parameters.</param>
/// <param name="RefKind">How the delegate returns: by value, <c>ref</c>, or <c>ref readonly</c>.</param>
/// <param name="Parameters">The parameters.</param>
/// <param name="TypeParameters">The type parameters, constraints included.</param>
/// <param name="ContainingType">
///     The type this one is declared in, or <see langword="null"/> for a top-level delegate. A delegate rendered
///     on its own is written inside that chain, each outer type as given with this one as its only member.
/// </param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility: <c>unsafe</c>, <c>file</c>.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record DelegateDeclaration(
    TypeReference ReturnType,
    string Name,
    RefKind RefKind = RefKind.None,
    EquatableArray<ParameterDeclaration> Parameters = default,
    EquatableArray<TypeParameterDeclaration> TypeParameters = default,
    TypeDeclaration? ContainingType = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(Accessibility, Modifiers, DocumentationComment, Attributes);

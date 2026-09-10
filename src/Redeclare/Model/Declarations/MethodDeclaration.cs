using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a method, an explicit interface implementation, or a user-defined operator.
/// </summary>
/// <remarks>
///     <paramref name="Body"/> is the text, and the snippet says whether that text is one expression:
///     <see cref="Snippet.From(string)"/> is text as written and renders in braces,
///     <see cref="Snippet.Expression(string)"/> renders as <c>=&gt; expression;</c> or as a block with a
///     <c>return</c>, following the options' preference for methods. <see langword="null"/> renders <c>;</c>,
///     right for abstract, extern, partial and interface members.
/// </remarks>
/// <param name="ReturnType">The return type. A reference to <c>System.Void</c> for none.</param>
/// <param name="Name">The name, or <c>operator +</c> and the like for an operator.</param>
/// <param name="RefKind">
///     How the return is passed back: by value, <c>ref</c>, or <c>ref readonly</c>. <c>ref</c> needs C# 7 and
///     <c>ref readonly</c> C# 7.2.
/// </param>
/// <param name="Body">The body, statements or one expression, or <see langword="null"/> for none.</param>
/// <param name="Parameters">The parameters.</param>
/// <param name="TypeParameters">The type parameters, constraints included.</param>
/// <param name="ExplicitInterfaceSpecifier">The interface an explicit implementation names before the dot, if any.</param>
/// <param name="Accessibility">The accessibility.</param>
/// <param name="Modifiers">The modifiers other than accessibility.</param>
/// <param name="DocumentationComment">The documentation comment.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record MethodDeclaration(
    TypeReference ReturnType,
    string Name,
    RefKind RefKind = RefKind.None,
    Snippet? Body = null,
    EquatableArray<ParameterDeclaration> Parameters = default,
    EquatableArray<TypeParameterDeclaration> TypeParameters = default,
    TypeReference? ExplicitInterfaceSpecifier = null,
    Accessibility Accessibility = Accessibility.NotApplicable,
    Modifiers Modifiers = Modifiers.None,
    string? DocumentationComment = null,
    EquatableArray<AttributeSpecification> Attributes = default)
    : MemberDeclaration(DocumentationComment, Attributes, Accessibility, Modifiers);

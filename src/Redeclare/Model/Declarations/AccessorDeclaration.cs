using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a <c>get</c>, <c>set</c>, <c>init</c>, <c>add</c> or <c>remove</c> accessor.
/// </summary>
/// <remarks>
///     A <see langword="null"/> body is an auto accessor, <c>get;</c>, so <c>new AccessorDeclaration()</c> is one.
/// </remarks>
/// <param name="Body">
///     The body, from <see cref="Snippet.From(string)"/> to render in braces or
///     <see cref="Snippet.Expression(string)"/> to allow an arrow, or <see langword="null"/> for an auto accessor.
/// </param>
/// <param name="IsInitOnly">Whether a setter is <c>init</c>. Needs C# 9.</param>
/// <param name="Accessibility">The accessibility, when narrower than the property's. <c>NotApplicable</c> writes none.</param>
internal sealed record AccessorDeclaration(
    Snippet? Body = null,
    bool IsInitOnly = false,
    Accessibility Accessibility = Accessibility.NotApplicable);

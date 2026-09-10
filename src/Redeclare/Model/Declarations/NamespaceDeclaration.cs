using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a namespace declaration, a member of a file or of another namespace.
/// </summary>
/// <remarks>
///     A dotted name, <c>A.B</c>, is one declaration. Nesting is a namespace among <paramref name="Members"/>.
///     The global namespace is one with an empty <paramref name="Name"/>, so a type goes into whatever namespace
///     it came from with one <c>with</c>. Only a namespace that is the sole member of its file may render
///     file-scoped, as C# requires. Any other renders as a block, whatever the options ask. Namespaces have no
///     accessibility, modifiers, attributes or documentation, and those are fixed here.
/// </remarks>
/// <param name="Name">
///     The name, dotted for a nested namespace declared in one go. Empty is the global namespace: its members
///     stand at the top of the file with no <c>namespace</c> line.
/// </param>
/// <param name="Members">The types and namespaces declared in it.</param>
internal sealed record NamespaceDeclaration(
    string Name,
    EquatableArray<MemberDeclaration> Members = default)
    : MemberDeclaration(null, default, Accessibility.NotApplicable, Modifiers.None);

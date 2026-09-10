using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents an extension block, <c>extension&lt;T&gt;(IEnumerable&lt;T&gt; source) { ... }</c>: a receiver
///     and the members that extend it, inside a static class. Needs C# 14.
/// </summary>
/// <remarks>
///     The receiver is a parameter. Its name is what the members' bodies refer to, and an empty name is the
///     unnamed form, <c>extension(string)</c>, whose members must all be static. Members are methods and
///     properties. An extension block has no accessibility, modifiers, attributes or documentation of its own,
///     and those are fixed here.
/// </remarks>
/// <param name="Receiver">The receiver parameter: its type, its name or an empty name, and how it is passed.</param>
/// <param name="TypeParameters">The type parameters, constraints included.</param>
/// <param name="Members">The extension members, in render order.</param>
/// <param name="Overrides">Render options for this block and everything under it.</param>
internal sealed record ExtensionDeclaration(
    ParameterDeclaration Receiver,
    EquatableArray<TypeParameterDeclaration> TypeParameters = default,
    EquatableArray<MemberDeclaration> Members = default,
    RenderOverrides? Overrides = null)
    : MemberDeclaration(null, default, Accessibility.NotApplicable, Modifiers.None, Overrides);

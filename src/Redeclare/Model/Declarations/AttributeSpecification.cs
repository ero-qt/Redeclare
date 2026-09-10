namespace Redeclare;

/// <summary>
///     Represents an attribute placed on a declaration: <c>[Target: Type(Arguments)]</c>. The C# specification
///     calls this an attribute specification.
/// </summary>
/// <remarks>
///     Arguments are snippets, positional first, then named as <c>Name = value</c>. The attribute type renders
///     by its full name, <c>Attribute</c> suffix included, which is always valid.
/// </remarks>
/// <param name="Type">The attribute type.</param>
/// <param name="Arguments">The arguments, positional first, then named.</param>
/// <param name="Target">The target specifier (<c>return</c>, <c>field</c>, <c>assembly</c>), if any.</param>
internal sealed record AttributeSpecification(
    TypeReference Type,
    EquatableArray<Snippet> Arguments = default,
    string? Target = null);

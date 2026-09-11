using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Provides the reads a generator makes when it adds members to a type it did not write.
/// </summary>
internal static class SymbolExtensions
{
    /// <summary>
    ///     The type as a new partial part of itself: its shape, none of the members or attributes a second part may
    ///     not repeat, and the containing types it is nested in. Add members to the result and render it.
    /// </summary>
    /// <param name="type">The type to write another part of.</param>
    /// <returns>A declaration with <c>partial</c> and nothing of its own.</returns>
    public static TypeDeclaration ToPart(this INamedTypeSymbol type)
    {
        var shape = type.ToDeclaration(ReadOptions.Shape);

        return shape with { Modifiers = shape.Modifiers | Modifiers.Partial };
    }
}

using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Provides the conversions from Roslyn's symbols to the model.
/// </summary>
internal static class RoslynExtensions
{
    /// <summary>
    ///     Reads a type symbol into a type reference.
    /// </summary>
    public static TypeReference ToTypeReference(this ITypeSymbol type)
    {
        return SymbolReader.ReadTypeReference(type);
    }
}

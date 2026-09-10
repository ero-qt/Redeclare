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

    /// <summary>
    ///     Reads an attribute into a specification, or <see langword="null"/> for one the compiler emitted.
    /// </summary>
    public static AttributeSpecification? ToSpecification(this AttributeData attribute)
    {
        return SymbolReader.ReadAttribute(attribute);
    }
}

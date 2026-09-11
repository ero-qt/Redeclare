using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Redeclare;

/// <summary>
///     Provides methods that find attributes on symbols by their class. Resolve the class once with
///     <c>Compilation.GetTypeByMetadataName</c> and compare symbols.
/// </summary>
internal static class AttributeExtensions
{
    /// <summary>
    ///     A typed view over the attribute's arguments.
    /// </summary>
    public static AttributeArguments GetArguments(this AttributeData attribute)
    {
        return new(attribute);
    }

    /// <summary>
    ///     Gets a value indicating whether the attribute is of <paramref name="attributeClass"/>. A constructed
    ///     generic attribute matches its definition.
    /// </summary>
    public static bool IsOfClass(this AttributeData attribute, INamedTypeSymbol attributeClass)
    {
        return attribute.AttributeClass is { } actual && actual.Is(attributeClass);
    }

    /// <summary>
    ///     The first attribute of this class on the symbol, or <see langword="null"/>.
    /// </summary>
    public static AttributeData? GetAttribute(this ISymbol symbol, INamedTypeSymbol attributeClass)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.IsOfClass(attributeClass))
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>
    ///     Every attribute of this class on the symbol.
    /// </summary>
    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, INamedTypeSymbol attributeClass)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.IsOfClass(attributeClass))
            {
                yield return attribute;
            }
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Redeclare;

/// <summary>
///     Provides the conversions between Roslyn's types and the model: symbols to declarations and type
///     references, a file to <c>SourceText</c>, and a language version to a <see cref="CSharpVersion"/>.
/// </summary>
internal static class RoslynExtensions
{
    /// <summary>
    ///     Reads a type symbol into a declaration. See <see cref="SymbolReader.ReadType"/>.
    /// </summary>
    public static TypeDeclaration ToDeclaration(this INamedTypeSymbol type, ReadOptions? options = null)
    {
        return SymbolReader.ReadType(type, options);
    }

    /// <summary>
    ///     Reads an extension block symbol into a declaration. See <see cref="SymbolReader.ReadExtension"/>.
    /// </summary>
    public static ExtensionDeclaration ToExtensionDeclaration(this INamedTypeSymbol extension, ReadOptions? options = null)
    {
        return SymbolReader.ReadExtension(extension, options);
    }

    /// <summary>
    ///     Reads a namespace symbol as a declaration with nothing in it, to put a type back where it lives:
    ///     <c>type.ContainingNamespace.ToDeclaration() with { Members = [declaration] }</c>. The global namespace
    ///     reads as one with no name, which writes no <c>namespace</c> line, so that line needs no check.
    /// </summary>
    public static NamespaceDeclaration ToDeclaration(this INamespaceSymbol @namespace)
    {
        return SymbolReader.ReadNamespace(@namespace);
    }

    /// <summary>
    ///     Reads a method symbol into a declaration with no body. See <see cref="SymbolReader.ReadMethod"/>.
    /// </summary>
    public static MethodDeclaration ToDeclaration(this IMethodSymbol method, ReadOptions? options = null)
    {
        return SymbolReader.ReadMethod(method, options);
    }

    /// <summary>
    ///     Reads a property symbol into a declaration with auto accessors.
    /// </summary>
    public static PropertyDeclaration ToDeclaration(this IPropertySymbol property, ReadOptions? options = null)
    {
        return SymbolReader.ReadProperty(property, options);
    }

    /// <summary>
    ///     Reads a field symbol into a declaration.
    /// </summary>
    public static FieldDeclaration ToDeclaration(this IFieldSymbol field, ReadOptions? options = null)
    {
        return SymbolReader.ReadField(field, options);
    }

    /// <summary>
    ///     Reads an event symbol into a declaration.
    /// </summary>
    public static EventDeclaration ToDeclaration(this IEventSymbol @event, ReadOptions? options = null)
    {
        return SymbolReader.ReadEvent(@event, options);
    }

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

    /// <summary>
    ///     Renders a file to a <see cref="SourceText"/> ready for <c>AddSource</c>.
    /// </summary>
    public static SourceText ToSourceText(this CompilationUnit unit, RenderOptions? options = null)
    {
        return SourceText.From(unit.Render(options), Encoding.UTF8);
    }

    /// <summary>
    ///     Maps a Roslyn language version, <c>Latest</c> and <c>Default</c> included, to the version it means. A
    ///     generator takes it from <c>context.ParseOptionsProvider</c>, which changes when the project's language
    ///     version does:
    ///     <c>RenderOptions.Default with { Version = ((CSharpParseOptions)parseOptions).LanguageVersion.ToCSharpVersion() }</c>.
    /// </summary>
    public static CSharpVersion ToCSharpVersion(this LanguageVersion version)
    {
        var effective = version.MapSpecifiedToEffectiveVersion();

        return (CSharpVersion)(int)effective;
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;

namespace Redeclare;

/// <summary>
///     Provides the conversions between Roslyn's types and the model: symbols to declarations and type
///     references, a file to <c>SourceText</c>, and a language version to a <see cref="CSharpVersion"/>.
/// </summary>
internal static class RoslynExtensions
{
    /// <summary>
    ///     Reads any symbol that declares a member into whichever declaration it is, the way
    ///     <see cref="TypeDeclaration.Members"/> is read: a type, delegate, extension block, method, constructor,
    ///     operator, property, field, enum member or event. A symbol with no declaration of its own, such as a
    ///     property's getter or a parameter, is an <see cref="ArgumentException"/>.
    /// </summary>
    public static MemberDeclaration ToDeclaration(this ISymbol symbol, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadMember(symbol)
            ?? throw new ArgumentException($"'{symbol.Name}' is not a member declaration.", nameof(symbol));
    }

    /// <summary>
    ///     Reads a named type symbol into whichever declaration it is: a <see cref="TypeDeclaration"/>, a
    ///     <see cref="DelegateDeclaration"/> or an <see cref="ExtensionDeclaration"/>. The three <c>To*Declaration</c>
    ///     methods read a symbol whose kind is known and keep the type.
    /// </summary>
    public static MemberDeclaration ToDeclaration(this INamedTypeSymbol type, ReadOptions? options = null)
    {
        var reader = SymbolReader.Create(options);

        return type switch
        {
            { IsExtension: true } => reader.ReadExtension(type),
            { TypeKind: TypeKind.Delegate } => reader.ReadDelegate(type),
            _ => reader.ReadType(type),
        };
    }

    /// <summary>
    ///     Reads a class, struct, interface, enum or record symbol into a declaration. See <see cref="SymbolReader.ReadType"/>.
    /// </summary>
    public static TypeDeclaration ToTypeDeclaration(this INamedTypeSymbol type, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadType(type);
    }

    /// <summary>
    ///     Reads a delegate symbol into a declaration. See <see cref="SymbolReader.ReadDelegate"/>.
    /// </summary>
    public static DelegateDeclaration ToDelegateDeclaration(this INamedTypeSymbol type, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadDelegate(type);
    }

    /// <summary>
    ///     Reads an extension block symbol into a declaration. See <see cref="SymbolReader.ReadExtension"/>.
    /// </summary>
    public static ExtensionDeclaration ToExtensionDeclaration(this INamedTypeSymbol extension, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadExtension(extension);
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
    ///     Reads a method symbol into a declaration with no body. See <see cref="SymbolReader.ReadMethod"/>. A
    ///     constructor reads through <see cref="ToConstructorDeclaration"/>.
    /// </summary>
    public static MethodDeclaration ToDeclaration(this IMethodSymbol method, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadMethod(method);
    }

    /// <summary>
    ///     Reads a constructor symbol into a declaration. See <see cref="SymbolReader.ReadConstructor"/>.
    /// </summary>
    public static ConstructorDeclaration ToConstructorDeclaration(this IMethodSymbol constructor, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadConstructor(constructor);
    }

    /// <summary>
    ///     Reads a property symbol into a declaration with auto accessors.
    /// </summary>
    public static PropertyDeclaration ToDeclaration(this IPropertySymbol property, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadProperty(property);
    }

    /// <summary>
    ///     Reads a field symbol into a declaration. An enum member reads through
    ///     <see cref="ToEnumMemberDeclaration"/>.
    /// </summary>
    public static FieldDeclaration ToDeclaration(this IFieldSymbol field, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadField(field);
    }

    /// <summary>
    ///     Reads an enum member symbol into a declaration with its value. See <see cref="SymbolReader.ReadEnumMember"/>.
    /// </summary>
    public static EnumMemberDeclaration ToEnumMemberDeclaration(this IFieldSymbol field, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadEnumMember(field);
    }

    /// <summary>
    ///     Reads a parameter symbol into a declaration. See <see cref="SymbolReader.ReadParameter"/>.
    /// </summary>
    public static ParameterDeclaration ToDeclaration(this IParameterSymbol parameter, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadParameter(parameter);
    }

    /// <summary>
    ///     Reads a type parameter symbol into a declaration with its constraints. See <see cref="SymbolReader.ReadTypeParameter"/>.
    /// </summary>
    public static TypeParameterDeclaration ToDeclaration(this ITypeParameterSymbol typeParameter, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadTypeParameter(typeParameter);
    }

    /// <summary>
    ///     Reads an event symbol into a declaration.
    /// </summary>
    public static EventDeclaration ToDeclaration(this IEventSymbol @event, ReadOptions? options = null)
    {
        return SymbolReader.Create(options).ReadEvent(@event);
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

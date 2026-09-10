using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Globalization;
using System.Text;

namespace Redeclare;

/// <summary>
///     Provides the conversions between Roslyn's types and the model: symbols to declarations and type
///     references, a file to <c>SourceText</c>, and a language version or an editorconfig to
///     <see cref="RenderOptions"/>.
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

    /// <summary>
    ///     The style an editorconfig describes, as options: indentation, line ending, namespace style, expression
    ///     body preferences, predefined type keywords. Keys it does not set keep the value they have in
    ///     <see cref="RenderOptions.Default"/>, and anything else is a <c>with</c> on the result. Pass the options
    ///     for the syntax tree being generated for, since editorconfig values are per file.
    /// </summary>
    public static RenderOptions ToRenderOptions(this AnalyzerConfigOptions config)
    {
        var result = RenderOptions.Default;

        if (Value(config, "indent_style") is { } indentStyle)
        {
            if (indentStyle == "TAB")
            {
                result = result with { Indent = "\t" };
            }
            else if (indentStyle == "SPACE")
            {
                int size = Int(config, "indent_size") ?? Int(config, "tab_width") ?? 4;
                result = result with { Indent = new string(' ', size) };
            }
        }
        else if (Int(config, "indent_size") is { } indentSize)
        {
            result = result with { Indent = new string(' ', indentSize) };
        }

        result = Value(config, "end_of_line") switch
        {
            "LF" => result with { NewLine = "\n" },
            "CRLF" => result with { NewLine = "\r\n" },
            "CR" => result with { NewLine = "\r" },
            _ => result,
        };

        result = Value(config, "csharp_style_namespace_declarations") switch
        {
            "FILE_SCOPED" => result with { NamespaceDeclarations = NamespaceDeclarationPreference.FileScoped },
            "BLOCK_SCOPED" => result with { NamespaceDeclarations = NamespaceDeclarationPreference.BlockScoped },
            _ => result,
        };

        if (Bool(config, "dotnet_style_predefined_type_for_locals_parameters_members") is { } keywords)
        {
            result = result with { PredefinedTypeKeywords = keywords };
        }

        if (Preference(config, "csharp_style_expression_bodied_methods") is { } methods)
        {
            result = result with { Methods = methods };
        }

        if (Preference(config, "csharp_style_expression_bodied_constructors") is { } constructors)
        {
            result = result with { Constructors = constructors };
        }

        if (Preference(config, "csharp_style_expression_bodied_properties") is { } properties)
        {
            result = result with { Properties = properties };
        }

        if (Preference(config, "csharp_style_expression_bodied_indexers") is { } indexers)
        {
            result = result with { Indexers = indexers };
        }

        if (Preference(config, "csharp_style_expression_bodied_accessors") is { } accessors)
        {
            result = result with { Accessors = accessors };
        }

        return result;
    }

    /// <summary>
    ///     The value before any <c>:severity</c> suffix, upper-cased, or <see langword="null"/>.
    /// </summary>
    private static string? Value(AnalyzerConfigOptions config, string key)
    {
        if (!config.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        int colon = raw.IndexOf(':');
        var value = colon < 0 ? raw : raw.Substring(0, colon);

        return value.Trim().ToUpperInvariant();
    }

    private static int? Int(AnalyzerConfigOptions config, string key)
    {
        return int.TryParse(Value(config, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : null;
    }

    private static bool? Bool(AnalyzerConfigOptions config, string key)
    {
        return Value(config, key) switch
        {
            "TRUE" => true,
            "FALSE" => false,
            _ => null,
        };
    }

    private static ExpressionBodyPreference? Preference(AnalyzerConfigOptions config, string key)
    {
        return Value(config, key) switch
        {
            "TRUE" => ExpressionBodyPreference.WhenPossible,
            "FALSE" => ExpressionBodyPreference.Never,
            "WHEN_ON_SINGLE_LINE" => ExpressionBodyPreference.WhenOnSingleLine,
            _ => null,
        };
    }
}

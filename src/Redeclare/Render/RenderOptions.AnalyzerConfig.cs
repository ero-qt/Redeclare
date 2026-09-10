using Microsoft.CodeAnalysis.Diagnostics;
using System.Globalization;

namespace Redeclare;

internal sealed partial record RenderOptions
{
    /// <summary>
    ///     The style an editorconfig describes, as options: indentation, line ending, namespace style, expression
    ///     body preferences, predefined type keywords. Keys it does not set keep the value they have in
    ///     <see cref="Default"/>, and anything else is a <c>with</c> on the result. Pass the options for the syntax
    ///     tree being generated for, since editorconfig values are per file.
    /// </summary>
    /// <param name="config">The options for one file, from <c>AnalyzerConfigOptionsProvider.GetOptions</c>.</param>
    /// <returns>The options the editorconfig describes.</returns>
    public static RenderOptions From(AnalyzerConfigOptions config)
    {
        var result = Default;

        if (GetValue(config, "indent_style") is { } indentStyle)
        {
            if (indentStyle == "TAB")
            {
                result = result with { Indent = "\t" };
            }
            else if (indentStyle == "SPACE")
            {
                int size = GetInt32(config, "indent_size") ?? GetInt32(config, "tab_width") ?? 4;
                result = result with { Indent = new string(' ', size) };
            }
        }
        else if (GetInt32(config, "indent_size") is { } indentSize)
        {
            result = result with { Indent = new string(' ', indentSize) };
        }

        result = GetValue(config, "end_of_line") switch
        {
            "LF" => result with { NewLine = "\n" },
            "CRLF" => result with { NewLine = "\r\n" },
            "CR" => result with { NewLine = "\r" },
            _ => result,
        };

        result = GetValue(config, "csharp_style_namespace_declarations") switch
        {
            "FILE_SCOPED" => result with { NamespaceDeclarations = NamespaceDeclarationPreference.FileScoped },
            "BLOCK_SCOPED" => result with { NamespaceDeclarations = NamespaceDeclarationPreference.BlockScoped },
            _ => result,
        };

        if (GetBoolean(config, "dotnet_style_predefined_type_for_locals_parameters_members") is { } keywords)
        {
            result = result with { PredefinedTypeKeywords = keywords };
        }

        if (GetPreference(config, "csharp_style_expression_bodied_methods") is { } methods)
        {
            result = result with { Methods = methods };
        }

        if (GetPreference(config, "csharp_style_expression_bodied_constructors") is { } constructors)
        {
            result = result with { Constructors = constructors };
        }

        if (GetPreference(config, "csharp_style_expression_bodied_properties") is { } properties)
        {
            result = result with { Properties = properties };
        }

        if (GetPreference(config, "csharp_style_expression_bodied_indexers") is { } indexers)
        {
            result = result with { Indexers = indexers };
        }

        if (GetPreference(config, "csharp_style_expression_bodied_accessors") is { } accessors)
        {
            result = result with { Accessors = accessors };
        }

        return result;
    }

    /// <summary>
    ///     The value before any <c>:severity</c> suffix, upper-cased, or <see langword="null"/>.
    /// </summary>
    private static string? GetValue(AnalyzerConfigOptions config, string key)
    {
        if (!config.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        int colon = raw.IndexOf(':');
        var value = colon < 0 ? raw : raw.Substring(0, colon);

        return value.Trim().ToUpperInvariant();
    }

    private static int? GetInt32(AnalyzerConfigOptions config, string key)
    {
        return int.TryParse(GetValue(config, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0
            ? value
            : null;
    }

    private static bool? GetBoolean(AnalyzerConfigOptions config, string key)
    {
        return GetValue(config, key) switch
        {
            "TRUE" => true,
            "FALSE" => false,
            _ => null,
        };
    }

    private static ExpressionBodyPreference? GetPreference(AnalyzerConfigOptions config, string key)
    {
        return GetValue(config, key) switch
        {
            "TRUE" => ExpressionBodyPreference.WhenPossible,
            "FALSE" => ExpressionBodyPreference.Never,
            "WHEN_ON_SINGLE_LINE" => ExpressionBodyPreference.WhenOnSingleLine,
            _ => null,
        };
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Redeclare;

internal sealed partial record RenderOptions
{
    /// <summary>
    ///     The defaults at the language version <paramref name="parseOptions"/> sets, for a generator that renders
    ///     one style everywhere and only needs the version to be right.
    /// </summary>
    /// <param name="parseOptions">The parse options, from <c>ParseOptionsProvider</c>.</param>
    /// <returns>The options to render under.</returns>
    public static RenderOptions From(ParseOptions parseOptions)
    {
        return Default with { Version = ToCSharpVersion(parseOptions) };
    }

    /// <summary>
    ///     The style <paramref name="config"/> describes, at the language version <paramref name="parseOptions"/>
    ///     sets. Editorconfig values are per file, so pass the options for the file being generated for.
    /// </summary>
    /// <param name="config">The options for one file, from <c>AnalyzerConfigOptionsProvider.GetOptions</c>.</param>
    /// <param name="parseOptions">The parse options, from <c>ParseOptionsProvider</c>.</param>
    /// <returns>The options to render that file under.</returns>
    public static RenderOptions From(AnalyzerConfigOptions config, ParseOptions parseOptions)
    {
        return From(config) with { Version = ToCSharpVersion(parseOptions) };
    }

    /// <summary>
    ///     The language version <paramref name="parseOptions"/> sets, as the renderer's version. A project that is
    ///     not C# renders at the newest, since nothing it parses with says otherwise.
    /// </summary>
    private static CSharpVersion ToCSharpVersion(ParseOptions parseOptions)
    {
        return parseOptions is CSharpParseOptions csharp ? csharp.LanguageVersion.ToCSharpVersion() : CSharpVersion.Latest;
    }
}

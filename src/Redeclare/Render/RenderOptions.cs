namespace Redeclare;

/// <summary>
///     C# language versions, numbered as Roslyn's <c>LanguageVersion</c> numbers them, so one converts to
///     the other by cast.
/// </summary>
/// <remarks>
///     A separate enum, so that the renderer can name versions the consumer's Roslyn does not know yet. Every
///     value here is a resolved version that orders correctly, where <c>LanguageVersion.Default</c> and
///     <c>Latest</c> are placeholders.
/// </remarks>
internal enum CSharpVersion
{
    /// <summary>
    ///     C# 1.
    /// </summary>
    CSharp1 = 1,

    /// <summary>
    ///     C# 2.
    /// </summary>
    CSharp2 = 2,

    /// <summary>
    ///     C# 3.
    /// </summary>
    CSharp3 = 3,

    /// <summary>
    ///     C# 4.
    /// </summary>
    CSharp4 = 4,

    /// <summary>
    ///     C# 5.
    /// </summary>
    CSharp5 = 5,

    /// <summary>
    ///     C# 6.
    /// </summary>
    CSharp6 = 6,

    /// <summary>
    ///     C# 7.0.
    /// </summary>
    CSharp7 = 7,

    /// <summary>
    ///     C# 7.1.
    /// </summary>
    CSharp7_1 = 701,

    /// <summary>
    ///     C# 7.2.
    /// </summary>
    CSharp7_2 = 702,

    /// <summary>
    ///     C# 7.3.
    /// </summary>
    CSharp7_3 = 703,

    /// <summary>
    ///     C# 8.
    /// </summary>
    CSharp8 = 800,

    /// <summary>
    ///     C# 9.
    /// </summary>
    CSharp9 = 900,

    /// <summary>
    ///     C# 10.
    /// </summary>
    CSharp10 = 1000,

    /// <summary>
    ///     C# 11.
    /// </summary>
    CSharp11 = 1100,

    /// <summary>
    ///     C# 12.
    /// </summary>
    CSharp12 = 1200,

    /// <summary>
    ///     C# 13.
    /// </summary>
    CSharp13 = 1300,

    /// <summary>
    ///     C# 14.
    /// </summary>
    CSharp14 = 1400,

    /// <summary>
    ///     Whatever is newest. Every feature is allowed.
    /// </summary>
    Latest = int.MaxValue,
}

/// <summary>
///     How type references are qualified: with namespace and <c>global::</c> prefix, with namespace alone, or
///     with neither.
/// </summary>
internal enum Qualification
{
    /// <summary>
    ///     <c>global::System.String</c>, the default. Correct in any file whatever its usings.
    /// </summary>
    Global = 0,

    /// <summary>
    ///     <c>System.String</c>.
    /// </summary>
    Full,

    /// <summary>
    ///     <c>String</c>. The file's usings must cover it.
    /// </summary>
    Minimal,
}

/// <summary>
///     Specifies when an expression body renders as <c>=&gt; expression;</c> rather than a block. The values
///     match what <c>csharp_style_expression_bodied_*</c> in an editorconfig accepts.
/// </summary>
internal enum ExpressionBodyPreference
{
    /// <summary>
    ///     Always a block. Editorconfig <c>false</c>.
    /// </summary>
    Never = 0,

    /// <summary>
    ///     Always an arrow. Editorconfig <c>true</c>.
    /// </summary>
    WhenPossible,

    /// <summary>
    ///     An arrow when the expression is on one line. Editorconfig <c>when_on_single_line</c>.
    /// </summary>
    WhenOnSingleLine,
}

/// <summary>
///     Specifies how a namespace declaration renders. The values match what
///     <c>csharp_style_namespace_declarations</c> in an editorconfig accepts.
/// </summary>
internal enum NamespaceDeclarationPreference
{
    /// <summary>
    ///     <c>namespace N { }</c>.
    /// </summary>
    BlockScoped = 0,

    /// <summary>
    ///     <c>namespace N;</c>. Needs C# 10, and degrades to block-scoped below it.
    /// </summary>
    FileScoped,
}

/// <summary>
///     Everything the renderer needs to decide, with a value for every field: the language version that
///     gates syntax, indentation and line endings, how types are spelled, and when members use expression
///     bodies.
/// </summary>
/// <remarks>
///     <para>
///         Style preferences degrade when the language version cannot express them: a file-scoped namespace
///         becomes block-scoped under C# 9, an arrow becomes a block under C# 5, a <c>?</c> on a reference type
///         disappears under C# 7. Features with meaning, a <c>record struct</c> under C# 9, throw
///         <see cref="RenderException"/>.
///     </para>
///     <para>
///         The defaults for expression bodies are Roslyn's defaults for the matching editorconfig keys. Every
///         field is set with <c>with</c>: <c>RenderOptions.Default with { Qualification = Qualification.Minimal }</c>.
///     </para>
/// </remarks>
/// <param name="Version">The language version that gates syntax.</param>
/// <param name="Indent">One level of indentation.</param>
/// <param name="NewLine">The line ending.</param>
/// <param name="Qualification">How type references are qualified.</param>
/// <param name="PredefinedTypeKeywords">Whether predefined types render as keywords (<c>int</c>) rather than names (<c>System.Int32</c>).</param>
/// <param name="NullableAnnotations">Whether reference types keep their <c>?</c>. Always off below C# 8.</param>
/// <param name="NamespaceDeclarations">The namespace declaration style.</param>
/// <param name="Methods">The expression body preference for methods and operators.</param>
/// <param name="Constructors">The expression body preference for constructors.</param>
/// <param name="Properties">The expression body preference for getter-only properties.</param>
/// <param name="Indexers">The expression body preference for getter-only indexers.</param>
/// <param name="Accessors">The expression body preference for accessors.</param>
/// <param name="BlankLineBetweenMembers">Whether a blank line separates members.</param>
internal sealed partial record RenderOptions(
    CSharpVersion Version = CSharpVersion.Latest,
    string Indent = "    ",
    string NewLine = "\n",
    Qualification Qualification = Qualification.Global,
    bool PredefinedTypeKeywords = true,
    bool NullableAnnotations = true,
    NamespaceDeclarationPreference NamespaceDeclarations = NamespaceDeclarationPreference.FileScoped,
    ExpressionBodyPreference Methods = ExpressionBodyPreference.Never,
    ExpressionBodyPreference Constructors = ExpressionBodyPreference.Never,
    ExpressionBodyPreference Properties = ExpressionBodyPreference.WhenOnSingleLine,
    ExpressionBodyPreference Indexers = ExpressionBodyPreference.WhenOnSingleLine,
    ExpressionBodyPreference Accessors = ExpressionBodyPreference.WhenOnSingleLine,
    bool BlankLineBetweenMembers = true)
{
    /// <summary>
    ///     Gets the defaults.
    /// </summary>
    public static RenderOptions Default { get; } = new();

    /// <summary>
    ///     Gets a value indicating whether the version allows a feature introduced in <paramref name="since"/>.
    /// </summary>
    public bool Allows(CSharpVersion since)
    {
        return Version >= since;
    }
}

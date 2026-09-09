namespace Redeclare;

/// <summary>
///     C# language versions, numbered as Roslyn's <c>LanguageVersion</c> numbers them, so one converts to
///     the other by cast.
/// </summary>
/// <remarks>
///     A separate enum, so that the renderer can name versions the consumer's Roslyn does not: Roslyn 4.8 has
///     no <c>LanguageVersion.CSharp13</c>. Every value here is a resolved version that orders correctly, where
///     <c>LanguageVersion.Default</c> and <c>Latest</c> are placeholders.
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
///     Everything the renderer needs to decide, with a value for every field: the language version that
///     gates syntax, and how types are spelled.
/// </summary>
/// <remarks>
///     Style preferences degrade when the language version cannot express them: a <c>?</c> on a reference
///     type disappears under C# 7. Features with meaning throw <see cref="RenderException"/>. Every field is
///     set with <c>with</c>: <c>RenderOptions.Default with { Qualification = Qualification.Minimal }</c>.
/// </remarks>
/// <param name="Version">The language version that gates syntax.</param>
/// <param name="Qualification">How type references are qualified.</param>
/// <param name="PredefinedTypeKeywords">Whether predefined types render as keywords (<c>int</c>) rather than names (<c>System.Int32</c>).</param>
/// <param name="NullableAnnotations">Whether reference types keep their <c>?</c>. Always off below C# 8.</param>
internal sealed record RenderOptions(
    CSharpVersion Version = CSharpVersion.Latest,
    Qualification Qualification = Qualification.Global,
    bool PredefinedTypeKeywords = true,
    bool NullableAnnotations = true)
{
    /// <summary>
    ///     Gets the defaults.
    /// </summary>
    public static RenderOptions Default { get; } = new();

    /// <summary>
    ///     Returns these options with every set field of <paramref name="overrides"/> applied.
    /// </summary>
    public RenderOptions Apply(RenderOverrides? overrides)
    {
        if (overrides is null)
        {
            return this;
        }

        return this with
        {
            Version = overrides.Version ?? Version,
            Qualification = overrides.Qualification ?? Qualification,
            PredefinedTypeKeywords = overrides.PredefinedTypeKeywords ?? PredefinedTypeKeywords,
            NullableAnnotations = overrides.NullableAnnotations ?? NullableAnnotations,
        };
    }

    /// <summary>
    ///     Gets a value indicating whether the version allows a feature introduced in <paramref name="since"/>.
    /// </summary>
    public bool Allows(CSharpVersion since)
    {
        return Version >= since;
    }
}

/// <summary>
///     <see cref="RenderOptions"/> with every field optional, to change how one part of the output renders.
///     The renderer applies it over the inherited options.
/// </summary>
/// <param name="Version">The language version, when it differs from the inherited options.</param>
/// <param name="Qualification">How type references are qualified, when it differs.</param>
/// <param name="PredefinedTypeKeywords">Whether predefined types render as keywords, when it differs.</param>
/// <param name="NullableAnnotations">Whether reference types keep their <c>?</c>, when it differs.</param>
internal sealed record RenderOverrides(
    CSharpVersion? Version = null,
    Qualification? Qualification = null,
    bool? PredefinedTypeKeywords = null,
    bool? NullableAnnotations = null);

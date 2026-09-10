namespace Redeclare;

/// <summary>
///     Specifies how one hole in a <see cref="Snippet"/> renders its type, chosen by a format specifier.
/// </summary>
internal enum HoleFormat
{
    /// <summary>
    ///     Under the qualification of the options in force. No specifier.
    /// </summary>
    Inherit = 0,

    /// <summary>
    ///     <c>global::</c> qualified, whatever the options say. Specifier <c>g</c>.
    /// </summary>
    Global,

    /// <summary>
    ///     Namespace qualified without <c>global::</c>. Specifier <c>f</c>.
    /// </summary>
    Full,

    /// <summary>
    ///     Name and containing types only. Specifier <c>m</c>.
    /// </summary>
    Minimal,

    /// <summary>
    ///     The simple name alone, for building identifiers. Specifier <c>n</c>.
    /// </summary>
    NameOnly,
}

/// <summary>
///     Represents a slot in a snippet's text that the renderer fills with a type, qualified for the file it
///     lands in.
/// </summary>
/// <param name="Type">The type to fill in.</param>
/// <param name="Format">How the type is qualified, when the snippet says so itself.</param>
internal sealed record SnippetHole(
    TypeReference Type,
    HoleFormat Format = HoleFormat.Inherit);

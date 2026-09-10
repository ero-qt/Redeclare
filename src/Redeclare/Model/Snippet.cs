using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Redeclare;

/// <summary>
///     Represents C# text with typed holes: a body, an initializer, a default value, an attribute argument.
///     A hole is a <see cref="TypeReference"/> the renderer fills under the options in force where the
///     snippet lands, so a body can name a type without deciding how it is qualified.
/// </summary>
/// <remarks>
///     <para>
///         A plain string converts implicitly, so <c>Initializer = "1"</c> reads as it should. An
///         interpolated string goes through <see cref="SnippetHandler"/>: <c>Snippet.From($"new {type}()")</c>
///         keeps <c>type</c> as a hole, and formats other values the way C# literals read, <c>true</c>
///         rather than <c>True</c>, numbers in the invariant culture. Format specifiers refine a hole:
///         <c>{type:m}</c> forces minimal qualification for that one hole, <c>{type:n}</c> gives its bare
///         name for building identifiers, <c>{text:L}</c> writes a string as an escaped C# literal,
///         <c>{name:I}</c> writes an identifier with <c>@</c> if it is a keyword.
///     </para>
///     <para>
///         Text is dedented once on construction, as a raw string literal is, so a body written in one
///         comes out aligned. Snippets nest: interpolating one into another splices it, holes and all.
///         <see cref="Join"/> and <see cref="Concat(IEnumerable{Snippet})"/> build argument lists and statement
///         blocks. Equality is by content, holes included, so a snippet in a model keeps the model cacheable.
///     </para>
/// </remarks>
internal sealed record Snippet
{
    /// <summary>
    ///     Marks the start of a hole in a line. The hole's index follows, then <see cref="HoleEnd"/>. Both are
    ///     control characters, which no C# source contains.
    /// </summary>
    internal const char HoleStart = '\u0001';

    /// <summary>
    ///     Marks the end of a hole.
    /// </summary>
    internal const char HoleEnd = '\u0002';

    private Snippet(EquatableArray<string> lines, EquatableArray<SnippetHole> holes)
    {
        Lines = lines;
        Holes = holes;
    }

    /// <summary>
    ///     Gets the empty snippet.
    /// </summary>
    public static Snippet Empty { get; } = new(default, default);

    /// <summary>
    ///     Gets the dedented lines, holes encoded inline.
    /// </summary>
    public EquatableArray<string> Lines { get; }

    /// <summary>
    ///     Gets the holes, by index.
    /// </summary>
    public EquatableArray<SnippetHole> Holes { get; }

    /// <summary>
    ///     Gets a value indicating whether there is no text.
    /// </summary>
    public bool IsEmpty => Lines.IsEmpty;

    /// <summary>
    ///     Gets a value indicating whether the text is on one line.
    /// </summary>
    public bool IsSingleLine => Lines.Length <= 1;

    /// <summary>
    ///     Creates a snippet from text with no holes, dedented.
    /// </summary>
    public static Snippet From(string text)
    {
        return string.IsNullOrEmpty(text) ? Empty : new(Dedent(text), default);
    }

    /// <summary>
    ///     Creates a snippet from an interpolated string whose <see cref="TypeReference"/> values become holes.
    /// </summary>
    public static Snippet From(SnippetHandler handler)
    {
        return handler.ToSnippet();
    }

    /// <summary>
    ///     Creates a snippet that is nothing but a type reference, the same as <c>Snippet.From($"{type}")</c>,
    ///     for putting a bare type where a snippet is expected, such as an item in <see cref="Join"/>.
    /// </summary>
    public static Snippet From(TypeReference type, HoleFormat format = HoleFormat.Inherit)
    {
        return new([HoleText(0)], [new SnippetHole(Type: type, Format: format)]);
    }

    /// <summary>
    ///     Joins snippets on one line with a separator, renumbering holes.
    /// </summary>
    public static Snippet Join(string separator, IEnumerable<Snippet> parts)
    {
        SnippetHandler handler = new(0, 0);
        bool first = true;
        foreach (var part in parts)
        {
            if (!first)
            {
                handler.AppendLiteral(separator);
            }

            handler.AppendFormatted(part);
            first = false;
        }

        return handler.ToSnippet();
    }

    /// <summary>
    ///     Concatenates a snippet made from each item, each on its own line.
    /// </summary>
    public static Snippet Concat<T>(IEnumerable<T> items, Func<T, Snippet> select)
    {
        List<Snippet> parts = [];
        foreach (var item in items)
        {
            parts.Add(select(item));
        }

        return Concat(parts);
    }

    /// <summary>
    ///     Concatenates snippets line-wise, renumbering holes.
    /// </summary>
    public static Snippet Concat(IEnumerable<Snippet> parts)
    {
        List<string> lines = [];
        List<SnippetHole> holes = [];
        foreach (var part in parts)
        {
            int offset = holes.Count;
            foreach (var line in part.Lines)
            {
                lines.Add(offset == 0 ? line : Renumber(line, offset));
            }

            holes.AddRange(part.Holes);
        }

        return new(lines.ToEquatableArray(), holes.ToEquatableArray());
    }

    /// <summary>
    ///     Wraps a plain string, for text with no holes: <c>Initializer = "1"</c>. An interpolated string
    ///     assigned straight to a <see cref="Snippet"/> takes this conversion after formatting itself, so its
    ///     types become text. <c>Snippet.From($"...")</c> keeps them as holes.
    /// </summary>
    public static implicit operator Snippet(string text)
    {
        return From(text);
    }

    /// <summary>
    ///     Returns this snippet with another appended on new lines.
    /// </summary>
    public Snippet Append(Snippet other)
    {
        return Concat([this, other]);
    }

    /// <summary>
    ///     Renders the text with every hole filled under <paramref name="options"/>, lines separated by
    ///     <c>\n</c>.
    /// </summary>
    public string Render(RenderOptions options)
    {
        return new StringBuilder().AppendSnippet(this, options).ToString();
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Render(RenderOptions.Default);
    }

    internal static Snippet Build(string text, List<SnippetHole> holes)
    {
        return new(Dedent(text), holes.ToEquatableArray());
    }

    /// <summary>
    ///     Encodes a hole reference for the given index.
    /// </summary>
    internal static string HoleText(int index)
    {
        return HoleStart + index.ToString(CultureInfo.InvariantCulture) + HoleEnd;
    }

    /// <summary>
    ///     Reads the index between a <see cref="HoleStart"/> at <paramref name="start"/> and the
    ///     <see cref="HoleEnd"/> at <paramref name="end"/>.
    /// </summary>
    internal static int HoleIndex(string line, int start, int end)
    {
        int index = 0;
        for (int i = start + 1; i < end; i++)
        {
            index = (index * 10) + (line[i] - '0');
        }

        return index;
    }

    /// <summary>
    ///     Shifts every hole index in a line by <paramref name="offset"/>, for splicing.
    /// </summary>
    internal static string Renumber(string line, int offset)
    {
        int start = line.IndexOf(HoleStart);
        if (start < 0)
        {
            return line;
        }

        StringBuilder text = new(line.Length + 4);
        int position = 0;
        while (start >= 0)
        {
            // A start marker with no end is text the caller put there, and is written out as it came in.
            int end = line.IndexOf(HoleEnd, start);
            if (end < 0)
            {
                break;
            }

            text.Append(line, position, start - position)
                .Append(HoleStart)
                .Append(HoleIndex(line, start, end) + offset)
                .Append(HoleEnd);
            position = end + 1;
            start = line.IndexOf(HoleStart, position);
        }

        return text.Append(line, position, line.Length - position).ToString();
    }

    /// <summary>
    ///     Splits text into lines, drops leading and trailing blank lines, and removes the common leading
    ///     whitespace. Tabs count as one character, the same as the compiler's raw string literals.
    /// </summary>
    internal static EquatableArray<string> Dedent(string text)
    {
        var raw = text.Replace("\r\n", "\n").Split('\n');

        int first = 0;
        int last = raw.Length - 1;
        while (first <= last && string.IsNullOrWhiteSpace(raw[first]))
        {
            first++;
        }

        while (last >= first && string.IsNullOrWhiteSpace(raw[last]))
        {
            last--;
        }

        if (first > last)
        {
            return default;
        }

        int common = int.MaxValue;
        for (int i = first; i <= last; i++)
        {
            var line = raw[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int indent = 0;
            while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
            {
                indent++;
            }

            common = Math.Min(common, indent);
        }

        var lines = new string[last - first + 1];
        for (int i = first; i <= last; i++)
        {
            var line = raw[i];
            lines[i - first] = string.IsNullOrWhiteSpace(line) ? "" : line.Substring(common).TrimEnd();
        }

        return EquatableArray<string>.Own(lines);
    }
}

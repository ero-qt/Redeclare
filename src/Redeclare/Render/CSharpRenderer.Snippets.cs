using System.Text;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    /// <summary>
    ///     Appends a snippet with every hole filled under <paramref name="options"/>, lines separated by its
    ///     <see cref="RenderOptions.NewLine"/>. Holes are filled straight into the builder, so a hole costs no string
    ///     of its own.
    /// </summary>
    public static StringBuilder AppendSnippet(this StringBuilder text, Snippet snippet, RenderOptions options)
    {
        var lines = snippet.Lines;
        int indentStart = lines.Length > 1 ? LineStart(text) : text.Length;
        int indentEnd = lines.Length > 1 ? IndentEnd(text, indentStart) : indentStart;
        int hole = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                text.Append(options.NewLine);
                if (lines[i].Length > 0)
                {
                    CopyIndent(text, indentStart, indentEnd);
                }
            }

            text.AppendSnippetLine(lines[i], snippet.Holes, ref hole, options);
        }

        return text;
    }

    private static void CopyIndent(StringBuilder text, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            text.Append(text[i]);
        }
    }

    /// <summary>
    ///     The index where the last line of the buffer begins.
    /// </summary>
    private static int LineStart(StringBuilder text)
    {
        int start = text.Length;
        while (start > 0 && text[start - 1] is not ('\n' or '\r'))
        {
            start--;
        }

        return start;
    }

    /// <summary>
    ///     The index just past the leading whitespace of the line beginning at <paramref name="start"/>.
    /// </summary>
    private static int IndentEnd(StringBuilder text, int start)
    {
        int end = start;
        while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
        {
            end++;
        }

        return end;
    }

    /// <summary>
    ///     Writes a snippet into the writer one line at a time, each at the current depth, holes filled.
    /// </summary>
    private static void WriteSnippet(this SourceWriter writer, Snippet snippet)
    {
        var options = writer.Options;

        var lines = snippet.Lines;
        int hole = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                writer.BeginLine().AppendSnippetLine(lines[i], snippet.Holes, ref hole, options);
            }

            writer.EndLine();
        }
    }

    /// <summary>
    ///     Writes a snippet that continues the current line, such as an initializer. Its further lines start at the
    ///     writer's depth, so a multi-line value stays aligned under its member.
    /// </summary>
    private static void WriteInline(this SourceWriter writer, Snippet snippet)
    {
        var options = writer.Options;

        var lines = snippet.Lines;
        int hole = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                writer.EndLine();
            }

            if (lines[i].Length > 0)
            {
                writer.BeginLine().AppendSnippetLine(lines[i], snippet.Holes, ref hole, options);
            }
        }
    }

    /// <summary>
    ///     Appends one line of a snippet, filling each mark with the hole at <paramref name="next"/> and moving
    ///     <paramref name="next"/> past it. A mark with no hole left for it is written as-is.
    /// </summary>
    private static StringBuilder AppendSnippetLine(
        this StringBuilder text,
        string line,
        EquatableArray<SnippetHole> holes,
        ref int next,
        RenderOptions options)
    {
        int position = 0;
        int mark = line.IndexOf(Snippet.HoleMark);
        while (mark >= 0 && next < holes.Length)
        {
            text.Append(line, position, mark - position);
            text.AppendHole(holes[next++], options);
            position = mark + 1;
            mark = line.IndexOf(Snippet.HoleMark, position);
        }

        return text.Append(line, position, line.Length - position);
    }

    private static StringBuilder AppendHole(this StringBuilder text, SnippetHole hole, RenderOptions options)
    {
        return hole.Format switch
        {
            HoleFormat.Inherit => text.AppendType(hole.Type, options),
            HoleFormat.Global => text.AppendType(hole.Type, options with { Qualification = Qualification.Global }),
            HoleFormat.Full => text.AppendType(hole.Type, options with { Qualification = Qualification.Full }),
            HoleFormat.Minimal => text.AppendType(hole.Type, options with { Qualification = Qualification.Minimal }),
            HoleFormat.NameOnly => text.Append(SimpleName(hole.Type)),
            _ => throw new RenderException($"Unknown hole format {hole.Format}."),
        };
    }

    /// <summary>
    ///     The bare name a <c>{type:n}</c> hole writes, for the kinds that have one.
    /// </summary>
    private static string SimpleName(TypeReference type)
    {
        return type switch
        {
            NamedTypeReference named => named.Name,
            TypeParameterReference parameter => parameter.Name,
            _ => throw new RenderException($"A {type.GetType().Name} has no simple name to write for a {{type:n}} hole."),
        };
    }
}

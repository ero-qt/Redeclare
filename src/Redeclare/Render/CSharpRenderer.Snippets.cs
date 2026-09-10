using System.Text;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    /// <summary>
    ///     Appends a snippet with every hole filled under <paramref name="options"/>, lines separated by
    ///     <c>\n</c>. Holes are filled straight into the builder, so a hole costs no string of its own.
    /// </summary>
    public static StringBuilder AppendSnippet(this StringBuilder text, Snippet snippet, RenderOptions options)
    {
        var lines = snippet.Lines;
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                text.Append('\n');
            }

            text.AppendSnippetLine(lines[i], snippet.Holes, options);
        }

        return text;
    }

    private static StringBuilder AppendSnippetLine(this StringBuilder text, string line, EquatableArray<SnippetHole> holes, RenderOptions options)
    {
        int start = line.IndexOf(Snippet.HoleStart);
        if (start < 0)
        {
            return text.Append(line);
        }

        int position = 0;
        while (start >= 0)
        {
            int end = line.IndexOf(Snippet.HoleEnd, start);
            if (end < 0)
            {
                break;
            }

            text.Append(line, position, start - position);
            text.AppendHole(holes[Snippet.HoleIndex(line, start, end)], options);
            position = end + 1;
            start = line.IndexOf(Snippet.HoleStart, position);
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

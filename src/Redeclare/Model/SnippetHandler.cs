using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Redeclare;

/// <summary>
///     Provides the interpolated string handler behind <see cref="Snippet"/>. The compiler routes
///     <c>Snippet.From($"new {type}()")</c> here, so <c>type</c> becomes a hole instead of text, and the
///     format specifiers in the <see cref="Snippet"/> remarks apply.
/// </summary>
[InterpolatedStringHandler]
internal readonly ref struct SnippetHandler
{
    private readonly StringBuilder _text;
    private readonly List<SnippetHole> _holes;

    /// <summary>
    ///     Initializes a handler for the literal length and hole count the compiler reports.
    /// </summary>
    public SnippetHandler(int literalLength, int formattedCount)
    {
        _text = new StringBuilder(literalLength + (formattedCount * 24));
        _holes = new List<SnippetHole>(formattedCount);
    }

    /// <summary>
    ///     Appends literal text.
    /// </summary>
    public void AppendLiteral(string text)
    {
        _text.Append(text);
    }

    /// <summary>
    ///     Appends a type as a hole rendered under the inherited options.
    /// </summary>
    public void AppendFormatted(TypeReference type)
    {
        AppendFormatted(type, null);
    }

    /// <summary>
    ///     Appends a type as a hole. <paramref name="format"/> is <c>g</c>, <c>f</c>, <c>m</c> or <c>n</c>.
    /// </summary>
    public void AppendFormatted(TypeReference type, string? format)
    {
        // Ignores a format specifier it does not know, like any other interpolated string does.
        var holeFormat = format switch
        {
            "g" => HoleFormat.Global,
            "f" => HoleFormat.Full,
            "m" => HoleFormat.Minimal,
            "n" => HoleFormat.NameOnly,
            _ => HoleFormat.Inherit,
        };

        _text.Append(Snippet.HoleText(_holes.Count));
        _holes.Add(new SnippetHole(Type: type, Format: holeFormat));
    }

    /// <summary>
    ///     Splices another snippet in, holes and all. A snippet of several lines repeats the indentation of the line
    ///     it lands on, so arms or members composed into an indented position line up with the text around them.
    /// </summary>
    public void AppendFormatted(Snippet snippet)
    {
        var lines = snippet.Lines;
        int offset = _holes.Count;
        var indent = lines.Length > 1 ? CurrentIndent() : "";

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                _text.Append('\n');
                if (lines[i].Length > 0)
                {
                    _text.Append(indent);
                }
            }

            _text.Append(offset == 0 ? lines[i] : Snippet.Renumber(lines[i], offset));
        }

        _holes.AddRange(snippet.Holes);
    }

    /// <summary>
    ///     Appends text as-is.
    /// </summary>
    public void AppendFormatted(string? text)
    {
        _text.Append(text);
    }

    /// <summary>
    ///     Appends text with a format. <c>L</c> writes it as an escaped C# string literal, or <c>null</c> for a
    ///     null. <c>I</c> writes it as an identifier, prefixed <c>@</c> when it is a keyword. Any other format
    ///     appends the text as-is.
    /// </summary>
    public void AppendFormatted(string? text, string? format)
    {
        switch (format)
        {
            case "L":
            {
                _text.Append(text is null ? "null" : SymbolDisplay.FormatLiteral(text, quote: true));
                break;
            }
            case "I":
            {
                if (text is not null && SyntaxFacts.GetKeywordKind(text) != SyntaxKind.None)
                {
                    _text.Append('@');
                }

                _text.Append(text);
                break;
            }
            default:
            {
                _text.Append(text);
                break;
            }
        }
    }

    /// <summary>
    ///     Appends a value the way a C# literal reads: <c>true</c> rather than <c>True</c>, numbers in the
    ///     invariant culture.
    /// </summary>
    public void AppendFormatted<T>(T value)
    {
        switch (value)
        {
            case null:
            {
                _text.Append("null");
                break;
            }
            // Catches derived references. A `NamedTypeReference` matches this overload exactly and the `TypeReference`
            // one only by conversion, so the compiler picks this one. It is a hole all the same.
            case TypeReference type:
            {
                AppendFormatted(type, null);
                break;
            }
            case bool flag:
            {
                _text.Append(flag ? "true" : "false");
                break;
            }
            case IFormattable formattable:
            {
                _text.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            }
            default:
            {
                _text.Append(value.ToString());
                break;
            }
        }
    }

    /// <summary>
    ///     Returns the finished snippet.
    /// </summary>
    internal Snippet ToSnippet()
    {
        return Snippet.Build(_text.ToString(), _holes);
    }

    /// <summary>
    ///     The whitespace the line being written starts with, which the further lines of a spliced snippet repeat.
    /// </summary>
    private string CurrentIndent()
    {
        int start = _text.Length;
        while (start > 0 && _text[start - 1] != '\n')
        {
            start--;
        }

        int end = start;
        while (end < _text.Length && (_text[end] == ' ' || _text[end] == '\t'))
        {
            end++;
        }

        return end == start ? "" : _text.ToString(start, end - start);
    }
}

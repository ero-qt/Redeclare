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
        // An unknown specifier is ignored, as it is in any other interpolated string.
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
    ///     Splices another snippet in, holes and all.
    /// </summary>
    public void AppendFormatted(Snippet snippet)
    {
        int offset = _holes.Count;
        bool first = true;
        foreach (var line in snippet.Lines)
        {
            if (!first)
            {
                _text.Append('\n');
            }

            first = false;
            _text.Append(offset == 0 ? line : Snippet.Renumber(line, offset));
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
            // A derived reference, NamedTypeReference say, matches this overload exactly and the TypeReference one
            // only by conversion, so the compiler binds here. It is a hole all the same.
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
}

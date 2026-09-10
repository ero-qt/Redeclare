using System;
using System.Collections.Generic;
using System.Text;

namespace Redeclare;

/// <summary>
///     Provides an indentation-aware text writer. Lines never carry trailing whitespace, blank lines never carry
///     indentation, and text containing newlines is split so every line lands at the current depth.
/// </summary>
/// <remarks>
///     The renderer writes most of a line straight into the buffer: <see cref="BeginLine"/> writes the indentation
///     and hands the buffer out, the caller appends, and <see cref="EndLine"/> ends the line. The writer is also
///     usable on its own for the parts of a generator that still write text by hand.
/// </remarks>
internal sealed class SourceWriter
{
    private readonly StringBuilder _text = new();
    private readonly string _indent;
    private readonly string _newLine;

    private bool _atLineStart = true;
    private bool _lastLineBlank = true;

    /// <summary>
    ///     Initializes a writer using the indent and line ending of <paramref name="options"/>.
    /// </summary>
    public SourceWriter(RenderOptions options)
    {
        _indent = options.Indent;
        _newLine = options.NewLine;
    }

    /// <summary>
    ///     Initializes a writer with four-space indentation and <c>\n</c> line endings.
    /// </summary>
    public SourceWriter()
        : this(RenderOptions.Default) { }

    /// <summary>
    ///     Gets the current indentation depth.
    /// </summary>
    public int Depth { get; private set; }

    /// <summary>
    ///     Increases the depth by one.
    /// </summary>
    public void Indent()
    {
        Depth++;
    }

    /// <summary>
    ///     Decreases the depth by one.
    /// </summary>
    public void Unindent()
    {
        if (Depth == 0)
        {
            throw new InvalidOperationException("Cannot unindent past depth zero.");
        }

        Depth--;
    }

    /// <summary>
    ///     Writes the indentation if the line is empty and returns the buffer, for appending the rest of the line
    ///     without intermediate strings. <see cref="EndLine"/> ends it.
    /// </summary>
    public StringBuilder BeginLine()
    {
        if (_atLineStart)
        {
            for (int i = 0; i < Depth; i++)
            {
                _text.Append(_indent);
            }

            _atLineStart = false;
        }

        return _text;
    }

    /// <summary>
    ///     Ends the current line.
    /// </summary>
    public void EndLine()
    {
        _lastLineBlank = _atLineStart;
        _text.Append(_newLine);
        _atLineStart = true;
    }

    /// <summary>
    ///     Writes text on the current line, indenting first if the line is empty.
    /// </summary>
    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        int newline = text.IndexOf('\n');
        if (newline >= 0)
        {
            WriteLine(text.Substring(0, newline));
            Write(text.Substring(newline + 1));
            return;
        }

        BeginLine().Append(text);
    }

    /// <summary>
    ///     Ends the current line.
    /// </summary>
    public void WriteLine()
    {
        EndLine();
    }

    /// <summary>
    ///     Writes text and ends the line. Text with newlines becomes one line each, all indented.
    /// </summary>
    public void WriteLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            EndLine();
            return;
        }

        var trimmed = text.Replace("\r\n", "\n").TrimEnd('\n');
        int start = 0;
        while (true)
        {
            int newline = trimmed.IndexOf('\n', start);
            var line = newline < 0 ? trimmed.Substring(start) : trimmed.Substring(start, newline - start);

            if (!string.IsNullOrWhiteSpace(line))
            {
                Write(line.TrimEnd());
            }

            EndLine();

            if (newline < 0)
            {
                return;
            }

            start = newline + 1;
        }
    }

    /// <summary>
    ///     Writes each line at the current depth.
    /// </summary>
    public void WriteLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            WriteLine(line);
        }
    }

    /// <summary>
    ///     Ends the current line and adds one blank line, unless the last line was already blank.
    /// </summary>
    public void BlankLine()
    {
        if (!_atLineStart)
        {
            EndLine();
        }

        if (!_lastLineBlank && _text.Length > 0)
        {
            EndLine();
        }
    }

    /// <summary>
    ///     Opens a brace block: writes <paramref name="open"/> on its own line, indents, and on dispose
    ///     unindents and writes <paramref name="close"/>.
    /// </summary>
    public IDisposable Block(string open = "{", string close = "}")
    {
        WriteLine(open);
        Indent();

        return new BlockScope(this, close);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return _text.ToString();
    }

    private sealed class BlockScope(
        SourceWriter writer,
        string close)
        : IDisposable
    {
        private bool _closed;

        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            if (!writer._atLineStart)
            {
                writer.EndLine();
            }

            writer.Unindent();
            writer.WriteLine(close);
        }
    }
}

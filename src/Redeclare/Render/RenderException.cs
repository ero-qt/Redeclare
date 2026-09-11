using System;

namespace Redeclare;

/// <summary>
///     Thrown when a declaration is not one C# can write: an event with one accessor, a namespace inside a type, an
///     expression body with no expression. These are mistakes in the code that built the model. A feature the
///     target language version lacks is not one, since the consumer's compiler reports that better than a
///     generator can, so the renderer writes it and lets the compiler say so.
/// </summary>
internal sealed class RenderException : Exception
{
    /// <summary>
    ///     A render failure with no message.
    /// </summary>
    public RenderException() { }

    /// <summary>
    ///     A render failure with a message.
    /// </summary>
    public RenderException(string message)
        : base(message) { }

    /// <summary>
    ///     A render failure with a message and a cause.
    /// </summary>
    public RenderException(string message, Exception innerException)
        : base(message, innerException) { }
}

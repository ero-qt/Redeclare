using System;

namespace Redeclare;

/// <summary>
///     Reports a declaration that C# cannot write: a namespace inside a type, a ref property with a setter, an
///     expression body with no expression. These are mistakes in the code that built the model. A feature the target
///     language version lacks is not one. The consumer's compiler reports that better than a generator can, so the
///     renderer writes the feature and lets the compiler say so.
/// </summary>
internal sealed class RenderException : Exception
{
    /// <summary>
    ///     Initializes a render failure with no message.
    /// </summary>
    public RenderException() { }

    /// <summary>
    ///     Initializes a render failure with a message.
    /// </summary>
    public RenderException(string message)
        : base(message) { }

    /// <summary>
    ///     Initializes a render failure with a message and a cause.
    /// </summary>
    public RenderException(string message, Exception innerException)
        : base(message, innerException) { }
}

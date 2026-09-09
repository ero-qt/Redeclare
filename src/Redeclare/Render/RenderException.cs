using System;

namespace Redeclare;

/// <summary>
///     Thrown when a model asks for something the render options cannot express without changing its
///     meaning, such as a <c>record struct</c> under C# 9.
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

    /// <summary>
    ///     A failure naming the feature, what needed it, and the version it needs.
    /// </summary>
    internal static RenderException Needs(string feature, string where, CSharpVersion since, CSharpVersion actual)
    {
        return new(
            $"{where} uses {feature}, which needs {Describe(since)}; the render options say {Describe(actual)}. "
            + "Raise the language version or change the model.");
    }

    private static string Describe(CSharpVersion version)
    {
        if (version == CSharpVersion.Latest)
        {
            return "the latest C#";
        }

        int value = (int)version;
        if (value < 100)
        {
            return $"C# {value}";
        }

        int major = value / 100;
        int minor = value % 100;
        return minor == 0 ? $"C# {major}" : $"C# {major}.{minor}";
    }
}

namespace Redeclare;

/// <summary>
///     Represents what a method is called: an identifier, an operator token, a conversion, or a destructor. A
///     <see cref="string"/> converts to an <see cref="Ordinary"/> name, so <c>Name: "Run"</c> reads as before.
/// </summary>
/// <remarks>
///     <see cref="ToString"/> writes the name the way C# does after the return type: <c>Run</c>, <c>operator +</c>,
///     <c>operator checked +=</c>, <c>implicit operator</c>, <c>~</c>. The renderer places the return type and the
///     containing type's name where each kind needs them.
/// </remarks>
internal abstract record MethodName
{
    private MethodName()
    {
    }

    public static implicit operator MethodName(string name)
    {
        return new Ordinary(name);
    }

    /// <summary>
    ///     Represents an identifier.
    /// </summary>
    /// <param name="Name">The identifier.</param>
    public sealed record Ordinary(string Name) : MethodName
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    ///     Represents a user-defined operator, <c>operator +</c>. Compound assignment operators, <c>operator +=</c>,
    ///     and the instance forms of <c>++</c> and <c>--</c> are operators too. Needs C# 14 for those.
    /// </summary>
    /// <param name="Token">The token after <c>operator</c>: <c>+</c>, <c>&gt;&gt;&gt;=</c>, <c>true</c>.</param>
    /// <param name="IsChecked">Whether the operator is the <c>checked</c> form. Needs C# 11.</param>
    public sealed record Operator(string Token, bool IsChecked = false) : MethodName
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            return IsChecked ? "operator checked " + Token : "operator " + Token;
        }
    }

    /// <summary>
    ///     Represents a conversion, <c>implicit operator</c> or <c>explicit operator</c>. The target type is the
    ///     method's return type.
    /// </summary>
    /// <param name="IsImplicit">Whether the conversion is <c>implicit</c>.</param>
    /// <param name="IsChecked">Whether an explicit conversion is the <c>checked</c> form. Needs C# 11.</param>
    public sealed record Conversion(bool IsImplicit, bool IsChecked = false) : MethodName
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            return (IsImplicit ? "implicit operator" : "explicit operator") + (IsChecked ? " checked" : "");
        }
    }

    /// <summary>
    ///     Represents a destructor, <c>~Name()</c>. The name is the containing type's, and there is no return type.
    /// </summary>
    public sealed record Destructor : MethodName
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            return "~";
        }
    }
}

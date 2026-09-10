namespace EnumHelpers.Consumer;

/// <summary>
///     A colour. <c>Azure</c> aliases <c>Blue</c>, which the generator must not turn into a second switch arm.
/// </summary>
[EnumHelpers]
public enum Color
{
    /// <summary>
    ///     Red.
    /// </summary>
    Red,

    /// <summary>
    ///     Green.
    /// </summary>
    Green,

    /// <summary>
    ///     Blue.
    /// </summary>
    Blue = 2,

    /// <summary>
    ///     Also blue.
    /// </summary>
    Azure = Blue,
}

/// <summary>
///     A byte-backed enum with a renamed helper class.
/// </summary>
[EnumHelpers(ClassName = "SizeHelpers")]
internal enum Size : byte
{
    /// <summary>
    ///     Small.
    /// </summary>
    Small = 1,

    /// <summary>
    ///     Large.
    /// </summary>
    Large = 2,
}

/// <summary>
///     Uses the generated helpers, so this project only compiles when the generator ran.
/// </summary>
public static class Demo
{
    /// <summary>
    ///     The colour's name, from the generated <c>ToStringFast</c>.
    /// </summary>
    /// <param name="color">The colour.</param>
    /// <returns>The member's name.</returns>
    public static string Name(Color color)
    {
        return color.ToStringFast();
    }

    /// <summary>
    ///     Whether the text names a size, from the generated <c>TryParse</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns><see langword="true"/> when the text names a member.</returns>
    public static bool IsSize(string text)
    {
        return SizeHelpers.TryParse(text, out var size) && size.IsDefined();
    }
}

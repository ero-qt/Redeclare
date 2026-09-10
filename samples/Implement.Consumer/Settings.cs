namespace Implement.Consumer;

/// <summary>
///     The definitions. The generator supplies the implementing declarations.
/// </summary>
public static partial class Settings
{
    /// <summary>
    ///     Gets the user's home directory, read from the environment.
    /// </summary>
    [FromEnvironment("HOME")]
    public static partial string? Home { get; }

    /// <summary>
    ///     Gets or sets a variable the program may also write.
    /// </summary>
    [FromEnvironment("IMPLEMENT_SAMPLE", Settable = true)]
    public static partial string? Sample { get; set; }

    /// <summary>
    ///     Writes its name and arguments.
    /// </summary>
    /// <param name="who">Who to greet.</param>
    /// <param name="times">How many times.</param>
    [Echo]
    public static partial void Greet(string who, int times);
}

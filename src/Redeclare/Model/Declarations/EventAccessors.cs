namespace Redeclare;

/// <summary>
///     Represents the <c>add</c> and <c>remove</c> accessors of an event. C# requires an event that declares one to
///     declare both, so they come as a pair.
/// </summary>
/// <param name="Add">The <c>add</c> accessor.</param>
/// <param name="Remove">The <c>remove</c> accessor.</param>
internal sealed record EventAccessors(AccessorDeclaration Add, AccessorDeclaration Remove)
{
    /// <summary>
    ///     Gets a pair of auto accessors, the shape a read event has before bodies are given.
    /// </summary>
    public static EventAccessors Auto { get; } = new(new AccessorDeclaration(), new AccessorDeclaration());
}

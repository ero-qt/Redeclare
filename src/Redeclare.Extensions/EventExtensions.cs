namespace Redeclare;

/// <summary>
///     Provides the edits a generator makes to an event it read before it writes the event back out.
/// </summary>
internal static class EventExtensions
{
    /// <summary>
    ///     Returns the event with bodies on its <c>add</c> and <c>remove</c> accessors. An event read from source has
    ///     <see cref="EventAccessors.Auto"/> and gains both bodies; a field-like event gains accessors.
    /// </summary>
    /// <param name="event">The event, as read from its definition.</param>
    /// <param name="add">The <c>add</c> body, or <see langword="null"/> to leave it as it is.</param>
    /// <param name="remove">The <c>remove</c> body, or <see langword="null"/> to leave it as it is.</param>
    /// <returns>The event with the bodies set.</returns>
    public static EventDeclaration WithBodies(this EventDeclaration @event, Snippet? add = null, Snippet? remove = null)
    {
        var accessors = @event.Accessors ?? EventAccessors.Auto;

        return @event with
        {
            Accessors = accessors with
            {
                Add = add is null ? accessors.Add : accessors.Add with { Body = add },
                Remove = remove is null ? accessors.Remove : accessors.Remove with { Body = remove },
            },
        };
    }
}

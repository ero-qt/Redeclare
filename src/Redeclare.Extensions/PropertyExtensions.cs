namespace Redeclare;

/// <summary>
///     Provides the edits a generator makes to a property it read before it writes the property back out.
/// </summary>
internal static class PropertyExtensions
{
    /// <summary>
    ///     The property with bodies on the accessors it has. Each accessor keeps its accessibility and <c>init</c>,
    ///     and only gains the body. A body for an accessor the property does not have is ignored.
    /// </summary>
    /// <param name="property">The property, as read from its definition.</param>
    /// <param name="getter">The getter's body, or <see langword="null"/> to leave it as it is.</param>
    /// <param name="setter">The setter's body, or <see langword="null"/> to leave it as it is.</param>
    /// <returns>The property with the bodies set.</returns>
    public static PropertyDeclaration WithBodies(this PropertyDeclaration property, Snippet? getter = null, Snippet? setter = null)
    {
        return property with
        {
            Getter = property.Getter is { } get && getter is not null ? get with { Body = getter } : property.Getter,
            Setter = property.Setter is { } set && setter is not null ? set with { Body = setter } : property.Setter,
        };
    }
}

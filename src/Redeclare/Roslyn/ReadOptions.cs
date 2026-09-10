namespace Redeclare;

/// <summary>
///     Specifies what <see cref="SymbolReader"/> reads along with a declaration's shape.
/// </summary>
/// <param name="IncludeMembers">Whether a type's members are read. Nested types are members.</param>
/// <param name="IncludeAttributes">Whether attributes are read. Attributes the compiler emits on its own are always dropped.</param>
/// <param name="IncludeDocumentationComments">
///     Whether documentation comments are read into <see cref="MemberDeclaration.DocumentationComment"/>.
/// </param>
/// <param name="IncludeImplicitlyDeclared">Whether implicitly declared members (record synthesis, default constructors) are read.</param>
internal sealed record ReadOptions(
    bool IncludeMembers = true,
    bool IncludeAttributes = true,
    bool IncludeDocumentationComments = false,
    bool IncludeImplicitlyDeclared = false)
{
    /// <summary>
    ///     Gets the defaults: members and attributes, no documentation, nothing implicit.
    /// </summary>
    public static ReadOptions Default { get; } = new();

    /// <summary>
    ///     Gets the options that read a type's shape and nothing else: no members, no attributes, no documentation.
    /// </summary>
    /// <remarks>
    ///     This is what a second part of a partial type is written from. An attribute read along with the shape is
    ///     written a second time, which is an error unless the attribute allows multiple, and the members of the part
    ///     are the ones being added rather than the ones already declared elsewhere.
    /// </remarks>
    public static ReadOptions Shape { get; } = new(IncludeMembers: false, IncludeAttributes: false);
}

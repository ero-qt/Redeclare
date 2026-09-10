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
}

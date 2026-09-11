namespace Redeclare;

/// <summary>
///     Provides the last step before a file is rendered under <see cref="Qualification.Minimal"/>, where every type
///     is written by its short name and the file carries the usings those names resolve through.
/// </summary>
internal static class CompilationUnitExtensions
{
    /// <summary>
    ///     The file with the usings its own declarations need, replacing whatever it carried. Minimal qualification
    ///     writes short names, so a namespace the walk misses is a name the consumer's compiler cannot resolve.
    /// </summary>
    /// <param name="unit">The file to collect over.</param>
    /// <returns>The file with its usings set.</returns>
    public static CompilationUnit WithCollectedUsings(this CompilationUnit unit)
    {
        return unit with { Usings = [.. CSharpRenderer.CollectNamespaces(unit)] };
    }
}

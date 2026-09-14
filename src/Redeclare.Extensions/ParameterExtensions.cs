using Microsoft.CodeAnalysis;
using System.Linq;

namespace Redeclare;

/// <summary>
///     Provides the argument list a generator writes when it forwards a call to a method it read.
/// </summary>
internal static class ParameterExtensions
{
    /// <summary>
    ///     Writes the argument that passes this parameter on: <c>ref x</c>, <c>out x</c>, <c>in x</c> for a
    ///     <c>ref readonly</c> parameter too, and the bare name for the rest. A name that is a keyword gets
    ///     <c>@</c>.
    /// </summary>
    public static Snippet ToArgument(this ParameterDeclaration parameter)
    {
        string modifier = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In or RefKind.RefReadOnlyParameter => "in ",
            _ => "",
        };

        return Snippet.Expression($"{modifier}{parameter.Name:I}");
    }

    /// <summary>
    ///     Writes the argument list that passes these parameters on, comma separated, without parentheses.
    /// </summary>
    public static Snippet ToArguments(this EquatableArray<ParameterDeclaration> parameters)
    {
        return Snippet.Join(", ", parameters.Select(ToArgument));
    }
}

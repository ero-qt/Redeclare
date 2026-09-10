using Microsoft.CodeAnalysis;

namespace Redeclare;

internal static partial class SymbolReader
{
    /// <summary>
    ///     The C# token for a user-defined operator's metadata name, or <see langword="null"/> for one with no
    ///     typed declaration: conversions and checked operators.
    /// </summary>
    private static string? OperatorToken(string metadataName)
    {
        return metadataName switch
        {
            WellKnownMemberNames.AdditionOperatorName => "+",
            WellKnownMemberNames.SubtractionOperatorName => "-",
            WellKnownMemberNames.MultiplyOperatorName => "*",
            WellKnownMemberNames.DivisionOperatorName => "/",
            WellKnownMemberNames.ModulusOperatorName => "%",
            WellKnownMemberNames.BitwiseAndOperatorName => "&",
            WellKnownMemberNames.BitwiseOrOperatorName => "|",
            WellKnownMemberNames.ExclusiveOrOperatorName => "^",
            WellKnownMemberNames.LeftShiftOperatorName => "<<",
            WellKnownMemberNames.RightShiftOperatorName => ">>",
            WellKnownMemberNames.UnsignedRightShiftOperatorName => ">>>",
            WellKnownMemberNames.EqualityOperatorName => "==",
            WellKnownMemberNames.InequalityOperatorName => "!=",
            WellKnownMemberNames.LessThanOperatorName => "<",
            WellKnownMemberNames.GreaterThanOperatorName => ">",
            WellKnownMemberNames.LessThanOrEqualOperatorName => "<=",
            WellKnownMemberNames.GreaterThanOrEqualOperatorName => ">=",
            WellKnownMemberNames.UnaryNegationOperatorName => "-",
            WellKnownMemberNames.UnaryPlusOperatorName => "+",
            WellKnownMemberNames.LogicalNotOperatorName => "!",
            WellKnownMemberNames.OnesComplementOperatorName => "~",
            WellKnownMemberNames.IncrementOperatorName => "++",
            WellKnownMemberNames.DecrementOperatorName => "--",
            WellKnownMemberNames.TrueOperatorName => "true",
            WellKnownMemberNames.FalseOperatorName => "false",
            _ => null,
        };
    }
}

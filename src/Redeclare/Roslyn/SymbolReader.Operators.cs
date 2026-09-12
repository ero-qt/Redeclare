using Microsoft.CodeAnalysis;

namespace Redeclare;

internal static partial class SymbolReader
{
    /// <summary>
    ///     The name a user-defined operator or conversion declares under, from its metadata name: <c>operator +</c>,
    ///     <c>operator checked +=</c>, <c>implicit operator</c>. A conversion's name has no type in it; the renderer
    ///     writes the return type there.
    /// </summary>
    private static string? GetOperatorName(string metadataName)
    {
        return metadataName switch
        {
            WellKnownMemberNames.ImplicitConversionName => "implicit operator",
            WellKnownMemberNames.ExplicitConversionName => "explicit operator",
            WellKnownMemberNames.CheckedExplicitConversionName => "explicit operator checked",
            _ => GetOperatorToken(metadataName) is { } token ? "operator " + token : null,
        };
    }

    /// <summary>
    ///     The C# token for a user-defined operator's metadata name, <c>checked</c> included, or
    ///     <see langword="null"/> for a name that is not an operator's.
    /// </summary>
    private static string? GetOperatorToken(string metadataName)
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
            WellKnownMemberNames.AdditionAssignmentOperatorName => "+=",
            WellKnownMemberNames.SubtractionAssignmentOperatorName => "-=",
            WellKnownMemberNames.MultiplicationAssignmentOperatorName => "*=",
            WellKnownMemberNames.DivisionAssignmentOperatorName => "/=",
            WellKnownMemberNames.ModulusAssignmentOperatorName => "%=",
            WellKnownMemberNames.BitwiseAndAssignmentOperatorName => "&=",
            WellKnownMemberNames.BitwiseOrAssignmentOperatorName => "|=",
            WellKnownMemberNames.ExclusiveOrAssignmentOperatorName => "^=",
            WellKnownMemberNames.LeftShiftAssignmentOperatorName => "<<=",
            WellKnownMemberNames.RightShiftAssignmentOperatorName => ">>=",
            WellKnownMemberNames.UnsignedRightShiftAssignmentOperatorName => ">>>=",
            WellKnownMemberNames.IncrementAssignmentOperatorName => "++",
            WellKnownMemberNames.DecrementAssignmentOperatorName => "--",
            WellKnownMemberNames.CheckedAdditionOperatorName => "checked +",
            WellKnownMemberNames.CheckedSubtractionOperatorName => "checked -",
            WellKnownMemberNames.CheckedMultiplyOperatorName => "checked *",
            WellKnownMemberNames.CheckedDivisionOperatorName => "checked /",
            WellKnownMemberNames.CheckedUnaryNegationOperatorName => "checked -",
            WellKnownMemberNames.CheckedIncrementOperatorName => "checked ++",
            WellKnownMemberNames.CheckedDecrementOperatorName => "checked --",
            WellKnownMemberNames.CheckedAdditionAssignmentOperatorName => "checked +=",
            WellKnownMemberNames.CheckedSubtractionAssignmentOperatorName => "checked -=",
            WellKnownMemberNames.CheckedMultiplicationAssignmentOperatorName => "checked *=",
            WellKnownMemberNames.CheckedDivisionAssignmentOperatorName => "checked /=",
            WellKnownMemberNames.CheckedIncrementAssignmentOperatorName => "checked ++",
            WellKnownMemberNames.CheckedDecrementAssignmentOperatorName => "checked --",
            _ => null,
        };
    }
}

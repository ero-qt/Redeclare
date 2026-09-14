using Microsoft.CodeAnalysis;

namespace Redeclare;

internal sealed partial class SymbolReader
{
    /// <summary>
    ///     The name a user-defined operator or conversion declares under, from its metadata name, or
    ///     <see langword="null"/> for a name that is not an operator's.
    /// </summary>
    private static MethodName? GetOperatorName(string metadataName)
    {
        return metadataName switch
        {
            WellKnownMemberNames.ImplicitConversionName => new MethodName.Conversion(IsImplicit: true),
            WellKnownMemberNames.ExplicitConversionName => new MethodName.Conversion(IsImplicit: false),
            WellKnownMemberNames.CheckedExplicitConversionName => new MethodName.Conversion(IsImplicit: false, IsChecked: true),
            _ => GetOperatorToken(metadataName) is var (token, isChecked) ? new MethodName.Operator(token, isChecked) : null,
        };
    }

    /// <summary>
    ///     The C# token for a user-defined operator's metadata name and whether it is the <c>checked</c> form, or
    ///     <see langword="null"/> for a name that is not an operator's.
    /// </summary>
    private static (string Token, bool IsChecked)? GetOperatorToken(string metadataName)
    {
        return metadataName switch
        {
            WellKnownMemberNames.AdditionOperatorName => ("+", false),
            WellKnownMemberNames.SubtractionOperatorName => ("-", false),
            WellKnownMemberNames.MultiplyOperatorName => ("*", false),
            WellKnownMemberNames.DivisionOperatorName => ("/", false),
            WellKnownMemberNames.ModulusOperatorName => ("%", false),
            WellKnownMemberNames.BitwiseAndOperatorName => ("&", false),
            WellKnownMemberNames.BitwiseOrOperatorName => ("|", false),
            WellKnownMemberNames.ExclusiveOrOperatorName => ("^", false),
            WellKnownMemberNames.LeftShiftOperatorName => ("<<", false),
            WellKnownMemberNames.RightShiftOperatorName => (">>", false),
            WellKnownMemberNames.UnsignedRightShiftOperatorName => (">>>", false),
            WellKnownMemberNames.EqualityOperatorName => ("==", false),
            WellKnownMemberNames.InequalityOperatorName => ("!=", false),
            WellKnownMemberNames.LessThanOperatorName => ("<", false),
            WellKnownMemberNames.GreaterThanOperatorName => (">", false),
            WellKnownMemberNames.LessThanOrEqualOperatorName => ("<=", false),
            WellKnownMemberNames.GreaterThanOrEqualOperatorName => (">=", false),
            WellKnownMemberNames.UnaryNegationOperatorName => ("-", false),
            WellKnownMemberNames.UnaryPlusOperatorName => ("+", false),
            WellKnownMemberNames.LogicalNotOperatorName => ("!", false),
            WellKnownMemberNames.OnesComplementOperatorName => ("~", false),
            WellKnownMemberNames.IncrementOperatorName => ("++", false),
            WellKnownMemberNames.DecrementOperatorName => ("--", false),
            WellKnownMemberNames.TrueOperatorName => ("true", false),
            WellKnownMemberNames.FalseOperatorName => ("false", false),
            WellKnownMemberNames.AdditionAssignmentOperatorName => ("+=", false),
            WellKnownMemberNames.SubtractionAssignmentOperatorName => ("-=", false),
            WellKnownMemberNames.MultiplicationAssignmentOperatorName => ("*=", false),
            WellKnownMemberNames.DivisionAssignmentOperatorName => ("/=", false),
            WellKnownMemberNames.ModulusAssignmentOperatorName => ("%=", false),
            WellKnownMemberNames.BitwiseAndAssignmentOperatorName => ("&=", false),
            WellKnownMemberNames.BitwiseOrAssignmentOperatorName => ("|=", false),
            WellKnownMemberNames.ExclusiveOrAssignmentOperatorName => ("^=", false),
            WellKnownMemberNames.LeftShiftAssignmentOperatorName => ("<<=", false),
            WellKnownMemberNames.RightShiftAssignmentOperatorName => (">>=", false),
            WellKnownMemberNames.UnsignedRightShiftAssignmentOperatorName => (">>>=", false),
            WellKnownMemberNames.IncrementAssignmentOperatorName => ("++", false),
            WellKnownMemberNames.DecrementAssignmentOperatorName => ("--", false),
            WellKnownMemberNames.CheckedAdditionOperatorName => ("+", true),
            WellKnownMemberNames.CheckedSubtractionOperatorName => ("-", true),
            WellKnownMemberNames.CheckedMultiplyOperatorName => ("*", true),
            WellKnownMemberNames.CheckedDivisionOperatorName => ("/", true),
            WellKnownMemberNames.CheckedUnaryNegationOperatorName => ("-", true),
            WellKnownMemberNames.CheckedIncrementOperatorName => ("++", true),
            WellKnownMemberNames.CheckedDecrementOperatorName => ("--", true),
            WellKnownMemberNames.CheckedAdditionAssignmentOperatorName => ("+=", true),
            WellKnownMemberNames.CheckedSubtractionAssignmentOperatorName => ("-=", true),
            WellKnownMemberNames.CheckedMultiplicationAssignmentOperatorName => ("*=", true),
            WellKnownMemberNames.CheckedDivisionAssignmentOperatorName => ("/=", true),
            WellKnownMemberNames.CheckedIncrementAssignmentOperatorName => ("++", true),
            WellKnownMemberNames.CheckedDecrementAssignmentOperatorName => ("--", true),
            _ => null,
        };
    }
}

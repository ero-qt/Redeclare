using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a type parameter with its variance and constraints. The renderer writes the <c>where</c>
///     clause from the flags in the order the grammar requires.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Variance">The variance. Only interfaces and delegates may declare one.</param>
/// <param name="HasReferenceTypeConstraint">Whether there is a <c>class</c> constraint.</param>
/// <param name="ReferenceTypeConstraintNullableAnnotation">Whether the <c>class</c> constraint is <c>class?</c>.</param>
/// <param name="HasValueTypeConstraint">
///     Whether there is a <c>struct</c> constraint. Implied by <paramref name="HasUnmanagedTypeConstraint"/>, which
///     renders instead.
/// </param>
/// <param name="HasUnmanagedTypeConstraint">Whether there is an <c>unmanaged</c> constraint.</param>
/// <param name="HasNotNullConstraint">Whether there is a <c>notnull</c> constraint.</param>
/// <param name="HasConstructorConstraint">Whether there is a <c>new()</c> constraint.</param>
/// <param name="AllowsRefLikeType">Whether there is an <c>allows ref struct</c> anti-constraint. Needs C# 13.</param>
/// <param name="ConstraintTypes">The base class and interface constraints.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record TypeParameterDeclaration(
    string Name,
    VarianceKind Variance = VarianceKind.None,
    bool HasReferenceTypeConstraint = false,
    NullableAnnotation ReferenceTypeConstraintNullableAnnotation = NullableAnnotation.None,
    bool HasValueTypeConstraint = false,
    bool HasUnmanagedTypeConstraint = false,
    bool HasNotNullConstraint = false,
    bool HasConstructorConstraint = false,
    bool AllowsRefLikeType = false,
    EquatableArray<TypeReference> ConstraintTypes = default,
    EquatableArray<AttributeSpecification> Attributes = default)
{
    /// <summary>
    ///     Gets a value indicating whether any constraint is set, which is whether a <c>where</c> clause renders.
    /// </summary>
    public bool HasConstraints => HasReferenceTypeConstraint || HasValueTypeConstraint || HasUnmanagedTypeConstraint
        || HasNotNullConstraint || HasConstructorConstraint || AllowsRefLikeType || !ConstraintTypes.IsEmpty;
}

using Microsoft.CodeAnalysis;
using System;

namespace Redeclare;

internal static partial class SymbolReader
{
    private static readonly SymbolDisplayFormat _qualified = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    ///     Reads a type reference: named types, arrays, pointers, function pointers, tuples, type parameters,
    ///     <c>dynamic</c>, and <c>Nullable&lt;T&gt;</c> as <c>T?</c>.
    /// </summary>
    public static TypeReference ReadTypeReference(ITypeSymbol type)
    {
        // Under a nullable context every type is NotAnnotated rather than None. The two render the same, and a model built
        // by hand says None, so reading says None too and the two compare equal.
        var annotation = type.NullableAnnotation == NullableAnnotation.Annotated ? NullableAnnotation.Annotated : NullableAnnotation.None;

        return type switch
        {
            IArrayTypeSymbol array => new ArrayTypeReference(
                ElementType: ReadTypeReference(array.ElementType),
                Rank: array.Rank,
                NullableAnnotation: annotation),
            IPointerTypeSymbol pointer => new PointerTypeReference(PointedAtType: ReadTypeReference(pointer.PointedAtType)),
            IFunctionPointerTypeSymbol functionPointer => ReadFunctionPointer(functionPointer.Signature),
            IDynamicTypeSymbol => new DynamicTypeReference(NullableAnnotation: annotation),
            ITypeParameterSymbol typeParameter => new TypeParameterReference(
                Name: typeParameter.Name,
                HasValueTypeConstraint: typeParameter.HasValueTypeConstraint,
                NullableAnnotation: annotation),
            INamedTypeSymbol named => ReadNamed(named, annotation),
            _ => new ErrorTypeReference(Text: type.ToDisplayString(_qualified)),
        };
    }

    private static TypeReference ReadNamed(INamedTypeSymbol named, NullableAnnotation annotation)
    {
        if (named.TypeKind == TypeKind.Error)
        {
            return new ErrorTypeReference(Text: named.ToDisplayString(_qualified));
        }

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1)
        {
            return ReadTypeReference(named.TypeArguments[0]) with { NullableAnnotation = NullableAnnotation.Annotated };
        }

        if (named.IsTupleType && named.TupleElements.Length > 0)
        {
            var elements = new TupleElement[named.TupleElements.Length];
            for (int i = 0; i < elements.Length; i++)
            {
                var element = named.TupleElements[i];
                elements[i] = new TupleElement(
                    Type: ReadTypeReference(element.Type),
                    Name: element.IsExplicitlyNamedTupleElement ? element.Name : null);
            }

            return new TupleTypeReference(Elements: elements, NullableAnnotation: annotation);
        }

        var typeArguments = EquatableArray<TypeReference>.Empty;
        if (!named.IsUnboundGenericType && named.TypeArguments.Length > 0)
        {
            var arguments = new TypeReference[named.TypeArguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                arguments[i] = ReadTypeReference(named.TypeArguments[i]);
            }

            typeArguments = arguments;
        }

        var containing = named.ContainingType is { } outer ? ReadNamed(outer, NullableAnnotation.None) as NamedTypeReference : null;
        string? ns = containing is null && named.ContainingNamespace is { IsGlobalNamespace: false } space
            ? space.ToDisplayString()
            : null;

        return new NamedTypeReference(
            Name: named.Name,
            ContainingNamespace: ns,
            ContainingType: containing,
            TypeKind: named.TypeKind,
            Arity: named.Arity,
            TypeArguments: typeArguments,
            SpecialType: named.SpecialType,
            IsNativeIntegerType: named.IsNativeIntegerType,
            NullableAnnotation: annotation);
    }

    /// <summary>
    ///     Reads a function pointer's signature. An unmanaged convention names itself through types called
    ///     <c>CallConvCdecl</c> and the like, which C# writes inside the brackets without that prefix.
    /// </summary>
    private static FunctionPointerTypeReference ReadFunctionPointer(IMethodSymbol signature)
    {
        var parameters = new FunctionPointerParameter[signature.Parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = signature.Parameters[i];
            parameters[i] = new FunctionPointerParameter(Type: ReadTypeReference(parameter.Type), RefKind: parameter.RefKind);
        }

        var conventions = new string[signature.UnmanagedCallingConventionTypes.Length];
        for (int i = 0; i < conventions.Length; i++)
        {
            string name = signature.UnmanagedCallingConventionTypes[i].Name;
            conventions[i] = name.StartsWith("CallConv", StringComparison.Ordinal) ? name.Substring("CallConv".Length) : name;
        }

        return new FunctionPointerTypeReference(
            ReturnType: ReadTypeReference(signature.ReturnType),
            RefKind: signature.RefKind,
            Parameters: parameters,
            CallingConvention: signature.CallingConvention,
            UnmanagedCallingConventionTypes: conventions);
    }
}

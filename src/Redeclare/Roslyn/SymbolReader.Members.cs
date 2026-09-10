using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Redeclare;

internal static partial class SymbolReader
{
    /// <summary>
    ///     Reads an ordinary method, an explicit implementation, or a user-defined operator. The body is left null.
    /// </summary>
    public static MethodDeclaration ReadMethod(IMethodSymbol method, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        string name = method.MethodKind switch
        {
            MethodKind.UserDefinedOperator => "operator " + GetOperatorToken(method.Name),
            MethodKind.ExplicitInterfaceImplementation when method.ExplicitInterfaceImplementations.Length > 0
                => method.ExplicitInterfaceImplementations[0].Name,
            _ => method.Name,
        };

        return new MethodDeclaration(
            DocumentationComment: ReadDocumentation(method, options),
            Attributes: ReadAttributes(method, options),
            Accessibility: ReadMemberAccessibility(method),
            Modifiers: ReadMemberModifiers(method),
            ReturnType: ReadTypeReference(method.ReturnType),
            RefKind: method.RefKind,
            Name: name,
            ExplicitInterfaceSpecifier: method.ExplicitInterfaceImplementations.Length > 0
                ? ReadTypeReference(method.ExplicitInterfaceImplementations[0].ContainingType)
                : null,
            TypeParameters: ReadTypeParameters(method.TypeParameters, options),
            Parameters: ReadParameters(method.Parameters, method.IsExtensionMethod, options));
    }

    /// <summary>
    ///     Reads a constructor. The body is an empty block.
    /// </summary>
    public static ConstructorDeclaration ReadConstructor(IMethodSymbol constructor, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        if (constructor.MethodKind is not (MethodKind.Constructor or MethodKind.StaticConstructor))
        {
            throw new ArgumentException($"'{constructor.Name}' is not a constructor.", nameof(constructor));
        }

        return new ConstructorDeclaration(
            DocumentationComment: ReadDocumentation(constructor, options),
            Attributes: ReadAttributes(constructor, options),
            Accessibility: constructor.IsStatic ? Accessibility.NotApplicable : constructor.DeclaredAccessibility,
            Modifiers: constructor.IsStatic ? Modifiers.Static : Modifiers.None,
            Parameters: ReadParameters(constructor.Parameters, isExtension: false, options),
            Body: Snippet.Empty);
    }

    /// <summary>
    ///     Reads a property or indexer, with auto accessors.
    /// </summary>
    public static PropertyDeclaration ReadProperty(IPropertySymbol property, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        var accessibility = ReadMemberAccessibility(property);
        var modifiers = ReadMemberModifiers(property);
        if (property.IsRequired)
        {
            modifiers |= Modifiers.Required;
        }

        // A struct member's own readonly lives on its accessors. Inside a readonly struct it is implied.
        bool accessorsReadOnly = property.GetMethod is null or { IsReadOnly: true }
            && property.SetMethod is null or { IsReadOnly: true };
        if (accessorsReadOnly && !property.IsStatic && property.ContainingType is { TypeKind: TypeKind.Struct, IsReadOnly: false })
        {
            modifiers |= Modifiers.ReadOnly;
        }

        var getter = property.GetMethod is { } get
            ? new AccessorDeclaration(Accessibility: ReadAccessorAccessibility(get, accessibility))
            : null;

        var setter = property.SetMethod is { } set
            ? new AccessorDeclaration(Accessibility: ReadAccessorAccessibility(set, accessibility), IsInitOnly: set.IsInitOnly)
            : null;

        return new PropertyDeclaration(
            DocumentationComment: ReadDocumentation(property, options),
            Attributes: ReadAttributes(property, options),
            Accessibility: accessibility,
            Modifiers: modifiers,
            Type: ReadTypeReference(property.Type),
            RefKind: property.RefKind,
            Name: property switch
            {
                { IsIndexer: true } => "this",
                { ExplicitInterfaceImplementations.Length: > 0 } => property.ExplicitInterfaceImplementations[0].Name,
                _ => property.Name,
            },
            ExplicitInterfaceSpecifier: property.ExplicitInterfaceImplementations.Length > 0
                ? ReadTypeReference(property.ExplicitInterfaceImplementations[0].ContainingType)
                : null,
            Parameters: property.IsIndexer ? ReadParameters(property.Parameters, isExtension: false, options) : default,
            Getter: getter,
            Setter: setter);
    }

    /// <summary>
    ///     Reads a field. A constant keeps its value as the initializer.
    /// </summary>
    public static FieldDeclaration ReadField(IFieldSymbol field, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        var modifiers = Modifiers.None;
        if (field.IsConst)
        {
            modifiers |= Modifiers.Const;
        }
        else if (field.IsStatic)
        {
            modifiers |= Modifiers.Static;
        }

        if (field.IsReadOnly)
        {
            modifiers |= Modifiers.ReadOnly;
        }

        if (field.IsVolatile)
        {
            modifiers |= Modifiers.Volatile;
        }

        if (field.IsRequired)
        {
            modifiers |= Modifiers.Required;
        }

        return new FieldDeclaration(
            DocumentationComment: ReadDocumentation(field, options),
            Attributes: ReadAttributes(field, options),
            Accessibility: field.DeclaredAccessibility,
            Modifiers: modifiers,
            Type: ReadTypeReference(field.Type),
            RefKind: field.RefKind,
            Name: field.Name,
            Initializer: field.IsConst && field.HasConstantValue ? FormatConstant(field.ConstantValue, field.Type) : null);
    }

    /// <summary>
    ///     Reads an enum member with its value.
    /// </summary>
    public static EnumMemberDeclaration ReadEnumMember(IFieldSymbol field, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        return new EnumMemberDeclaration(
            DocumentationComment: ReadDocumentation(field, options),
            Attributes: ReadAttributes(field, options),
            Name: field.Name,
            Value: field.HasConstantValue && field.ConstantValue is { } value
                ? Snippet.From(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0")
                : null);
    }

    /// <summary>
    ///     Reads a field-like event.
    /// </summary>
    public static EventDeclaration ReadEvent(IEventSymbol @event, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        return new EventDeclaration(
            DocumentationComment: ReadDocumentation(@event, options),
            Attributes: ReadAttributes(@event, options),
            Accessibility: ReadMemberAccessibility(@event),
            Modifiers: ReadMemberModifiers(@event),
            Type: ReadTypeReference(@event.Type),
            Name: @event.Name);
    }

    /// <summary>
    ///     Reads a parameter. <paramref name="isThis"/> marks the receiver of an extension method.
    /// </summary>
    public static ParameterDeclaration ReadParameter(IParameterSymbol parameter, bool isThis = false, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        return new ParameterDeclaration(
            Attributes: ReadAttributes(parameter, options),
            RefKind: parameter.RefKind,
            IsParams: parameter.IsParams,
            IsThis: isThis,
            IsScoped: parameter.ScopedKind != ScopedKind.None,
            Type: ReadTypeReference(parameter.Type),
            Name: parameter.Name,
            Default: parameter.HasExplicitDefaultValue ? FormatConstant(parameter.ExplicitDefaultValue, parameter.Type) : null);
    }

    private static EquatableArray<ParameterDeclaration> ReadParameters(
        ImmutableArray<IParameterSymbol> parameters,
        bool isExtension,
        ReadOptions options)
    {
        var result = new ParameterDeclaration[parameters.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ReadParameter(parameters[i], isThis: isExtension && i == 0, options);
        }

        return result;
    }

    private static EquatableArray<TypeParameterDeclaration> ReadTypeParameters(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        ReadOptions options)
    {
        var result = new TypeParameterDeclaration[typeParameters.Length];
        for (int i = 0; i < result.Length; i++)
        {
            var parameter = typeParameters[i];
            var constraintTypes = new TypeReference[parameter.ConstraintTypes.Length];
            for (int j = 0; j < constraintTypes.Length; j++)
            {
                constraintTypes[j] = ReadTypeReference(parameter.ConstraintTypes[j]);
            }

            // ITypeParameterSymbol.AllowsRefLikeType is a Roslyn 4.10 API, above the 4.8 these sources compile against.
            result[i] = new TypeParameterDeclaration(
                Name: parameter.Name,
                Variance: parameter.Variance,
                HasReferenceTypeConstraint: parameter.HasReferenceTypeConstraint,
                ReferenceTypeConstraintNullableAnnotation: parameter.ReferenceTypeConstraintNullableAnnotation,
                HasValueTypeConstraint: parameter.HasValueTypeConstraint,
                HasUnmanagedTypeConstraint: parameter.HasUnmanagedTypeConstraint,
                HasNotNullConstraint: parameter.HasNotNullConstraint,
                HasConstructorConstraint: parameter.HasConstructorConstraint,
                ConstraintTypes: constraintTypes,
                Attributes: ReadAttributes(parameter, options));
        }

        return result;
    }

    /// <summary>
    ///     Interface members are public by default, and writing it needs C# 8 for nothing. An explicit interface
    ///     implementation may carry no modifier at all, whatever Roslyn reports for it.
    /// </summary>
    private static Accessibility ReadMemberAccessibility(ISymbol member)
    {
        if (member.ContainingType is { TypeKind: TypeKind.Interface } && member.DeclaredAccessibility == Accessibility.Public)
        {
            return Accessibility.NotApplicable;
        }

        bool isExplicit = member switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Length > 0,
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Length > 0,
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.Length > 0,
            _ => false,
        };

        return isExplicit ? Accessibility.NotApplicable : member.DeclaredAccessibility;
    }

    private static Accessibility ReadAccessorAccessibility(IMethodSymbol accessor, Accessibility propertyAccessibility)
    {
        var accessibility = accessor.DeclaredAccessibility;

        return accessibility == propertyAccessibility || accessor.ContainingType is { TypeKind: TypeKind.Interface }
            ? Accessibility.NotApplicable
            : accessibility;
    }

    private static Modifiers ReadMemberModifiers(ISymbol member)
    {
        var modifiers = Modifiers.None;
        bool inInterface = member.ContainingType is { TypeKind: TypeKind.Interface };

        if (member.IsStatic)
        {
            modifiers |= Modifiers.Static;
        }

        if (member.IsAbstract && (!inInterface || member.IsStatic))
        {
            modifiers |= Modifiers.Abstract;
        }

        if (member.IsVirtual && !inInterface)
        {
            modifiers |= Modifiers.Virtual;
        }

        if (member.IsOverride)
        {
            modifiers |= Modifiers.Override;
        }

        if (member.IsSealed && !inInterface)
        {
            modifiers |= Modifiers.Sealed;
        }

        if (member.IsExtern)
        {
            modifiers |= Modifiers.Extern;
        }

        if (member is IMethodSymbol method)
        {
            if (method.IsAsync)
            {
                modifiers |= Modifiers.Async;
            }

            if (method.IsReadOnly && !method.IsStatic && method.ContainingType is { TypeKind: TypeKind.Struct, IsReadOnly: false })
            {
                modifiers |= Modifiers.ReadOnly;
            }

            if (method.IsPartialDefinition)
            {
                modifiers |= Modifiers.Partial;
            }
        }

        return modifiers;
    }
}

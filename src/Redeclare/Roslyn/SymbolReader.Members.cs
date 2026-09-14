using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Redeclare;

internal static partial class SymbolReader
{
    /// <summary>
    ///     Reads an ordinary method, an explicit implementation, a user-defined operator, a conversion or a finalizer.
    ///     The body is left null. <c>[return: ...]</c> attributes come along with <c>Target</c> set. A finalizer is
    ///     named <c>~Name</c>.
    /// </summary>
    public static MethodDeclaration ReadMethod(IMethodSymbol method, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        if (method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor)
        {
            throw new ArgumentException($"'{method.Name}' is a constructor. Use ToConstructorDeclaration.", nameof(method));
        }

        if (method.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion && GetOperatorName(method.Name) is null)
        {
            throw new ArgumentException($"'{method.Name}' is not the metadata name of an operator.", nameof(method));
        }

        return new MethodDeclaration(
            DocumentationComment: ReadDocumentation(method, options),
            Attributes: ReadAttributes(method, options),
            Accessibility: ReadMemberAccessibility(method),
            Modifiers: ReadMemberModifiers(method),
            ReturnType: ReadTypeReference(method.ReturnType),
            RefKind: method.RefKind,
            Name: ReadMethodName(method),
            ExplicitInterfaceSpecifier: method.ExplicitInterfaceImplementations.Length > 0
                ? ReadTypeReference(method.ExplicitInterfaceImplementations[0].ContainingType)
                : null,
            TypeParameters: ReadTypeParameters(method.TypeParameters, options),
            Parameters: ReadParameters(method.Parameters, method.IsExtensionMethod, options));
    }

    private static MethodName ReadMethodName(IMethodSymbol method)
    {
        var declared = method.ExplicitInterfaceImplementations.Length > 0 ? method.ExplicitInterfaceImplementations[0] : method;

        return declared.MethodKind switch
        {
            MethodKind.UserDefinedOperator or MethodKind.Conversion => GetOperatorName(declared.Name)!,
            MethodKind.Destructor => new MethodName.Destructor(),
            _ => declared.Name,
        };
    }

    /// <summary>
    ///     Reads a constructor. The body is an empty block, or none for a <c>partial</c> definition or an <c>extern</c>
    ///     constructor.
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
            Modifiers: ReadMemberModifiers(constructor),
            Parameters: ReadParameters(constructor.Parameters, isExtension: false, options),
            Body: constructor.IsPartialDefinition || constructor.IsExtern ? null : Snippet.Empty);
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

        bool propertyReadOnly = HasReadOnlyAccessors(property) && IsInstanceMemberOfMutableStruct(property);
        if (propertyReadOnly)
        {
            modifiers |= Modifiers.ReadOnly;
        }

        var getter = property.GetMethod is { } get ? ReadAccessor(get, accessibility, propertyReadOnly, options) : null;
        var setter = property.SetMethod is { } set ? ReadAccessor(set, accessibility, propertyReadOnly, options) : null;

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

        if (field.ContainingType is { TypeKind: TypeKind.Enum })
        {
            throw new ArgumentException($"'{field.Name}' is an enum member. Use ToEnumMemberDeclaration.", nameof(field));
        }

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

        if (MentionsPointer(field) || field.IsFixedSizeBuffer)
        {
            modifiers |= Modifiers.Unsafe;
        }

        var type = field.IsFixedSizeBuffer && field.Type is IPointerTypeSymbol pointer ? pointer.PointedAtType : field.Type;

        return new FieldDeclaration(
            DocumentationComment: ReadDocumentation(field, options),
            Attributes: ReadAttributes(field, options),
            Accessibility: field.DeclaredAccessibility,
            Modifiers: modifiers,
            Type: ReadTypeReference(type),
            RefKind: field.RefKind,
            FixedSize: field.FixedSize,
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
    ///     Reads an event. One with its own accessors comes back with both as shape and no bodies, and an explicit
    ///     interface implementation carries the interface and the bare name.
    /// </summary>
    public static EventDeclaration ReadEvent(IEventSymbol @event, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        bool hasAccessors = IsWrittenInSource(@event.AddMethod) || IsWrittenInSource(@event.RemoveMethod);
        var explicitInterface = @event.ExplicitInterfaceImplementations.Length > 0 ? @event.ExplicitInterfaceImplementations[0] : null;

        return new EventDeclaration(
            DocumentationComment: ReadDocumentation(@event, options),
            Attributes: ReadAttributes(@event, options),
            Accessibility: ReadMemberAccessibility(@event),
            Modifiers: ReadMemberModifiers(@event),
            Type: ReadTypeReference(@event.Type),
            Name: explicitInterface?.Name ?? @event.Name,
            Adder: hasAccessors ? new AccessorDeclaration() : null,
            Remover: hasAccessors ? new AccessorDeclaration() : null,
            ExplicitInterfaceSpecifier: explicitInterface is null ? null : ReadTypeReference(explicitInterface.ContainingType));
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
            IsScoped: HasExplicitScoped(parameter),
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

            result[i] = new TypeParameterDeclaration(
                Name: parameter.Name,
                Variance: parameter.Variance,
                HasReferenceTypeConstraint: parameter.HasReferenceTypeConstraint,
                ReferenceTypeConstraintNullableAnnotation: parameter.ReferenceTypeConstraintNullableAnnotation,
                HasValueTypeConstraint: parameter.HasValueTypeConstraint,
                HasUnmanagedTypeConstraint: parameter.HasUnmanagedTypeConstraint,
                HasNotNullConstraint: parameter.HasNotNullConstraint,
                HasConstructorConstraint: parameter.HasConstructorConstraint,
                AllowsRefLikeType: parameter.AllowsRefLikeType,
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
        bool publicInInterface = member.ContainingType is { TypeKind: TypeKind.Interface } && member.DeclaredAccessibility == Accessibility.Public;
        if (publicInInterface || member is IMethodSymbol { MethodKind: MethodKind.Destructor })
        {
            return Accessibility.NotApplicable;
        }

        return IsExplicitImplementation(member) ? Accessibility.NotApplicable : member.DeclaredAccessibility;
    }

    private static bool IsExplicitImplementation(ISymbol member)
    {
        return member switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Length > 0,
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Length > 0,
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.Length > 0,
            _ => false,
        };
    }

    /// <summary>
    ///     Whether the member's signature names a pointer, which is what <c>unsafe</c> is required for. A symbol
    ///     does not report the keyword, so it is read back from the types the member mentions.
    /// </summary>
    private static bool MentionsPointer(ISymbol member)
    {
        switch (member)
        {
            case IFieldSymbol field:
            {
                return MentionsPointer(field.Type);
            }
            case IEventSymbol @event:
            {
                return MentionsPointer(@event.Type);
            }
            case IPropertySymbol property:
            {
                return MentionsPointer(property.Type) || MentionsPointer(property.Parameters);
            }
            case IMethodSymbol method:
            {
                return MentionsPointer(method.ReturnType) || MentionsPointer(method.Parameters);
            }
            default:
            {
                return false;
            }
        }
    }

    private static bool MentionsPointer(ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (MentionsPointer(parameter.Type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MentionsPointer(ITypeSymbol type)
    {
        return type switch
        {
            IPointerTypeSymbol => true,
            IFunctionPointerTypeSymbol => true,
            IArrayTypeSymbol array => MentionsPointer(array.ElementType),
            _ => false,
        };
    }

    private static AccessorDeclaration ReadAccessor(IMethodSymbol accessor, Accessibility propertyAccessibility, bool propertyReadOnly, ReadOptions options)
    {
        return new AccessorDeclaration(
            Accessibility: ReadAccessorAccessibility(accessor, propertyAccessibility),
            IsInitOnly: accessor.IsInitOnly,
            IsReadOnly: !propertyReadOnly && HasWrittenReadOnly(accessor),
            Attributes: ReadAttributes(accessor, options));
    }

    /// <summary>
    ///     Checks for a <c>readonly</c> written on the accessor. The compiler marks every auto getter of a struct
    ///     readonly, so the symbol alone does not say whether the word is there.
    /// </summary>
    private static bool HasWrittenReadOnly(IMethodSymbol accessor)
    {
        foreach (var reference in accessor.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is AccessorDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static Accessibility ReadAccessorAccessibility(IMethodSymbol accessor, Accessibility propertyAccessibility)
    {
        bool narrows = accessor.DeclaredAccessibility != propertyAccessibility;
        bool explicitImplementation = propertyAccessibility == Accessibility.NotApplicable;
        bool inInterface = accessor.ContainingType is { TypeKind: TypeKind.Interface };

        return narrows && !explicitImplementation && !inInterface ? accessor.DeclaredAccessibility : Accessibility.NotApplicable;
    }

    private static bool HasReadOnlyAccessors(IPropertySymbol property)
    {
        return property.GetMethod is null or { IsReadOnly: true } && property.SetMethod is null or { IsReadOnly: true };
    }

    private static bool IsInstanceMemberOfMutableStruct(ISymbol member)
    {
        return !member.IsStatic && member.ContainingType is { TypeKind: TypeKind.Struct, IsReadOnly: false };
    }

    private static bool IsWrittenInSource(IMethodSymbol? accessor)
    {
        return accessor is { IsImplicitlyDeclared: false, DeclaringSyntaxReferences.Length: > 0 };
    }

    private static bool HasExplicitScoped(IParameterSymbol parameter)
    {
        bool implied = parameter.RefKind == RefKind.Out || (parameter.IsParams && parameter.Type.IsRefLikeType);

        return parameter.ScopedKind != ScopedKind.None && !implied;
    }

    private static bool IsPartialDefinition(ISymbol member)
    {
        return member switch
        {
            IMethodSymbol method => method.IsPartialDefinition,
            IPropertySymbol property => property.IsPartialDefinition,
            IEventSymbol @event => @event.IsPartialDefinition,
            _ => false,
        };
    }

    private static Modifiers ReadMemberModifiers(ISymbol member)
    {
        if (member is IMethodSymbol { MethodKind: MethodKind.Destructor })
        {
            return Modifiers.None;
        }

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

        if (member.IsVirtual && (!inInterface || member.IsStatic))
        {
            modifiers |= Modifiers.Virtual;
        }

        if (member.IsOverride)
        {
            modifiers |= Modifiers.Override;
        }

        if (member.IsSealed || IsSealedInterfaceMember(member))
        {
            modifiers |= Modifiers.Sealed;
        }

        if (member.IsExtern)
        {
            modifiers |= Modifiers.Extern;
        }

        if (MentionsPointer(member))
        {
            modifiers |= Modifiers.Unsafe;
        }

        if (IsPartialDefinition(member))
        {
            modifiers |= Modifiers.Partial;
        }

        if (member is IMethodSymbol method)
        {
            if (method.IsAsync)
            {
                modifiers |= Modifiers.Async;
            }

            if (method.IsReadOnly && IsInstanceMemberOfMutableStruct(method))
            {
                modifiers |= Modifiers.ReadOnly;
            }
        }

        return modifiers;
    }

    /// <summary>
    ///     Checks for <c>sealed</c> on an interface member. Roslyn reports <c>sealed void M() { }</c> in an interface
    ///     as neither virtual, abstract nor sealed, so the member is sealed when it is none of the three.
    /// </summary>
    private static bool IsSealedInterfaceMember(ISymbol member)
    {
        return member.ContainingType is { TypeKind: TypeKind.Interface }
            && !member.IsStatic
            && !member.IsVirtual
            && !member.IsAbstract
            && !IsExplicitImplementation(member);
    }
}

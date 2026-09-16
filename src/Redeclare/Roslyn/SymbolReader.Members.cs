using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Redeclare;

internal sealed partial class SymbolReader
{
    /// <summary>
    ///     Reads an ordinary method, an explicit implementation, a user-defined operator, a conversion or a finalizer.
    ///     The body is left null. <c>[return: ...]</c> attributes come along with <c>Target</c> set. A finalizer is
    ///     named <c>~Name</c>.
    /// </summary>
    public MethodDeclaration ReadMethod(IMethodSymbol method)
    {
        if (method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor)
        {
            throw new ArgumentException($"'{method.Name}' is a constructor. Use ToConstructorDeclaration.", nameof(method));
        }

        if (method.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion && GetOperatorName(method.Name) is null)
        {
            throw new ArgumentException($"'{method.Name}' is not the metadata name of an operator.", nameof(method));
        }

        var facts = Describe(method);

        return new MethodDeclaration(
            DocumentationComment: ReadDocumentation(method),
            Attributes: ReadAttributes(method),
            Accessibility: ReadMemberAccessibility(facts),
            Modifiers: ReadMemberModifiers(facts),
            ReturnType: ReadTypeReference(method.ReturnType),
            RefKind: method.RefKind,
            Name: ReadMethodName(facts),
            ExplicitInterfaceSpecifier: ReadExplicitInterfaceSpecifier(facts),
            TypeParameters: ReadTypeParameters(method.TypeParameters),
            Parameters: ReadParameters(method.Parameters, method.IsExtensionMethod));
    }

    private static MethodName ReadMethodName(MemberFacts facts)
    {
        var declared = (IMethodSymbol)facts.Declared;

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
    public ConstructorDeclaration ReadConstructor(IMethodSymbol constructor)
    {
        if (constructor.MethodKind is not (MethodKind.Constructor or MethodKind.StaticConstructor))
        {
            throw new ArgumentException($"'{constructor.Name}' is not a constructor.", nameof(constructor));
        }

        return new ConstructorDeclaration(
            DocumentationComment: ReadDocumentation(constructor),
            Attributes: ReadAttributes(constructor),
            Accessibility: constructor.IsStatic ? Accessibility.NotApplicable : constructor.DeclaredAccessibility,
            Modifiers: ReadMemberModifiers(Describe(constructor)),
            Parameters: ReadParameters(constructor.Parameters, isExtension: false),
            Body: constructor.IsPartialDefinition || constructor.IsExtern ? null : Snippet.Empty);
    }

    /// <summary>
    ///     Reads a property or indexer, with auto accessors.
    /// </summary>
    public PropertyDeclaration ReadProperty(IPropertySymbol property)
    {
        var facts = Describe(property);
        var modifiers = ReadMemberModifiers(facts);
        if (property.IsRequired)
        {
            modifiers |= Modifiers.Required;
        }

        bool propertyReadOnly = HasReadOnlyAccessors(property) && IsInstanceMemberOfMutableStruct(property);
        if (propertyReadOnly)
        {
            modifiers |= Modifiers.ReadOnly;
        }

        var getter = property.GetMethod is { } get ? ReadAccessor(get, facts, propertyReadOnly) : null;
        var setter = property.SetMethod is { } set ? ReadAccessor(set, facts, propertyReadOnly) : null;

        return new PropertyDeclaration(
            DocumentationComment: ReadDocumentation(property),
            Attributes: ReadAttributes(property),
            Accessibility: ReadMemberAccessibility(facts),
            Modifiers: modifiers,
            Type: ReadTypeReference(property.Type),
            RefKind: property.RefKind,
            Name: property.IsIndexer ? "this" : facts.Declared.Name,
            ExplicitInterfaceSpecifier: ReadExplicitInterfaceSpecifier(facts),
            Parameters: property.IsIndexer ? ReadParameters(property.Parameters, isExtension: false) : default,
            Getter: getter,
            Setter: setter);
    }

    /// <summary>
    ///     Reads a field. A constant keeps its value as the initializer.
    /// </summary>
    public FieldDeclaration ReadField(IFieldSymbol field)
    {
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
            DocumentationComment: ReadDocumentation(field),
            Attributes: ReadAttributes(field),
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
    public EnumMemberDeclaration ReadEnumMember(IFieldSymbol field)
    {
        return new EnumMemberDeclaration(
            DocumentationComment: ReadDocumentation(field),
            Attributes: ReadAttributes(field),
            Name: field.Name,
            Value: field.HasConstantValue && field.ConstantValue is { } value
                ? Snippet.From(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0")
                : null);
    }

    /// <summary>
    ///     Reads an event. One with its own accessors comes back with both as shape and no bodies, and an explicit
    ///     interface implementation carries the interface and the bare name.
    /// </summary>
    public EventDeclaration ReadEvent(IEventSymbol @event)
    {
        bool hasAccessors = IsWrittenInSource(@event.AddMethod) || IsWrittenInSource(@event.RemoveMethod);
        var facts = Describe(@event);

        return new EventDeclaration(
            DocumentationComment: ReadDocumentation(@event),
            Attributes: ReadAttributes(@event),
            Accessibility: ReadMemberAccessibility(facts),
            Modifiers: ReadMemberModifiers(facts),
            Type: ReadTypeReference(@event.Type),
            Name: facts.Declared.Name,
            Accessors: hasAccessors ? EventAccessors.Auto : null,
            ExplicitInterfaceSpecifier: ReadExplicitInterfaceSpecifier(facts));
    }

    /// <summary>
    ///     Reads a parameter. <paramref name="isThis"/> marks the receiver of an extension method.
    /// </summary>
    public ParameterDeclaration ReadParameter(IParameterSymbol parameter, bool isThis = false)
    {
        return new ParameterDeclaration(
            Attributes: ReadAttributes(parameter),
            RefKind: parameter.RefKind,
            IsParams: parameter.IsParams,
            IsThis: isThis,
            IsScoped: HasExplicitScoped(parameter),
            Type: ReadTypeReference(parameter.Type),
            Name: parameter.Name,
            Default: parameter.HasExplicitDefaultValue ? FormatConstant(parameter.ExplicitDefaultValue, parameter.Type) : null);
    }

    private EquatableArray<ParameterDeclaration> ReadParameters(
        ImmutableArray<IParameterSymbol> parameters,
        bool isExtension)
    {
        var result = new ParameterDeclaration[parameters.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = ReadParameter(parameters[i], isThis: isExtension && i == 0);
        }

        return result;
    }

    private EquatableArray<TypeParameterDeclaration> ReadTypeParameters(
        ImmutableArray<ITypeParameterSymbol> typeParameters)
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
                Attributes: ReadAttributes(parameter));
        }

        return result;
    }

    /// <summary>
    ///     Holds the facts about a member that accessibility, modifiers and names all turn on, computed once.
    /// </summary>
    /// <param name="Member">The member.</param>
    /// <param name="Declared">
    ///     The member whose name and kind the declaration carries: the interface member for an explicit
    ///     implementation, otherwise <paramref name="Member"/> itself.
    /// </param>
    /// <param name="InInterface">Whether the member is declared in an interface.</param>
    /// <param name="IsDestructor">Whether the member is a destructor, which carries no accessibility or modifiers.</param>
    private readonly record struct MemberFacts(ISymbol Member, ISymbol Declared, bool InInterface, bool IsDestructor)
    {
        public bool IsExplicitImplementation => !ReferenceEquals(Member, Declared);
    }

    private static MemberFacts Describe(ISymbol member)
    {
        bool isDestructor = member is IMethodSymbol { MethodKind: MethodKind.Destructor };
        var implemented = member switch
        {
            IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 } method => method.ExplicitInterfaceImplementations[0],
            IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 } property => property.ExplicitInterfaceImplementations[0],
            IEventSymbol { ExplicitInterfaceImplementations.Length: > 0 } @event => @event.ExplicitInterfaceImplementations[0],
            _ => (ISymbol?)null,
        };

        return new MemberFacts(
            Member: member,
            Declared: implemented ?? member,
            InInterface: member.ContainingType is { TypeKind: TypeKind.Interface },
            IsDestructor: isDestructor);
    }

    /// <summary>
    ///     Reads the accessibility a member is written with. An interface member is public by default, and writing
    ///     <c>public</c> needs C# 8 for nothing. An explicit interface implementation carries no accessibility at all,
    ///     whatever Roslyn reports for it.
    /// </summary>
    private static Accessibility ReadMemberAccessibility(MemberFacts facts)
    {
        bool publicInInterface = facts.InInterface && facts.Member.DeclaredAccessibility == Accessibility.Public;

        return publicInInterface || facts.IsDestructor || facts.IsExplicitImplementation
            ? Accessibility.NotApplicable
            : facts.Member.DeclaredAccessibility;
    }

    private static TypeReference? ReadExplicitInterfaceSpecifier(MemberFacts facts)
    {
        return facts.IsExplicitImplementation ? ReadTypeReference(facts.Declared.ContainingType) : null;
    }

    /// <summary>
    ///     Checks whether the member's signature names a pointer, which is what <c>unsafe</c> is required for. A symbol
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

    private AccessorDeclaration ReadAccessor(IMethodSymbol accessor, MemberFacts property, bool propertyReadOnly)
    {
        return new AccessorDeclaration(
            Accessibility: ReadAccessorAccessibility(accessor, property),
            IsInitOnly: accessor.IsInitOnly,
            IsReadOnly: !propertyReadOnly && HasWrittenReadOnly(accessor),
            Attributes: ReadAttributes(accessor));
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

    private static Accessibility ReadAccessorAccessibility(IMethodSymbol accessor, MemberFacts property)
    {
        bool narrows = accessor.DeclaredAccessibility != property.Member.DeclaredAccessibility;

        return narrows && !property.IsExplicitImplementation && !property.InInterface ? accessor.DeclaredAccessibility : Accessibility.NotApplicable;
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

    private static Modifiers ReadMemberModifiers(MemberFacts facts)
    {
        if (facts.IsDestructor)
        {
            return Modifiers.None;
        }

        var member = facts.Member;
        var modifiers = Modifiers.None;
        bool inInterface = facts.InInterface;

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

        if (member.IsSealed || IsSealedInterfaceMember(facts))
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
    ///     as neither virtual, abstract nor sealed, so the member is sealed when it is none of the three. A private
    ///     member cannot be overridden and takes no <c>sealed</c>.
    /// </summary>
    private static bool IsSealedInterfaceMember(MemberFacts facts)
    {
        return facts.InInterface
            && facts.Member.DeclaredAccessibility != Accessibility.Private
            && !facts.Member.IsStatic
            && !facts.Member.IsVirtual
            && !facts.Member.IsAbstract
            && !facts.IsExplicitImplementation;
    }
}

using System.Collections.Generic;

namespace Redeclare;

/// <summary>
///     Provides every type reference a declaration holds, for any pass over the model to filter.
/// </summary>
internal static class TypeReferenceExtensions
{
    /// <summary>
    ///     Gets every type reference the file's members hold, holes in snippets included. A reference comes back as
    ///     written, and <see cref="DescendantsAndSelf"/> opens it up.
    /// </summary>
    public static IEnumerable<TypeReference> GetTypeReferences(this CompilationUnit unit)
    {
        foreach (var member in unit.Members)
        {
            foreach (var type in member.GetTypeReferences())
            {
                yield return type;
            }
        }
    }

    /// <summary>
    ///     Gets every type reference the member holds, its own members' included.
    /// </summary>
    public static IEnumerable<TypeReference> GetTypeReferences(this MemberDeclaration member)
    {
        foreach (var type in GetTypeReferences(member.Attributes))
        {
            yield return type;
        }

        var own = member switch
        {
            NamespaceDeclaration ns => GetTypeReferences(ns.Members),
            TypeDeclaration type => GetTypeReferences(type),
            DelegateDeclaration @delegate => Concat(
                [@delegate.ReturnType],
                GetTypeReferences(@delegate.Parameters),
                GetTypeReferences(@delegate.TypeParameters),
                GetContainingTypeReferences(@delegate.ContainingType)),
            ExtensionDeclaration extension => Concat(
                GetTypeReferences(extension.Receiver),
                GetTypeReferences(extension.TypeParameters),
                GetTypeReferences(extension.Members)),
            MethodDeclaration method => Concat(
                [method.ReturnType],
                GetTypeReferences(method.Parameters),
                GetTypeReferences(method.TypeParameters),
                GetTypeReferences(method.Body),
                Optional(method.ExplicitInterfaceSpecifier)),
            ConstructorDeclaration constructor => Concat(
                GetTypeReferences(constructor.Parameters),
                GetTypeReferences(constructor.Body),
                GetTypeReferences(constructor.Initializer)),
            PropertyDeclaration property => Concat(
                [property.Type],
                GetTypeReferences(property.Parameters),
                GetTypeReferences(property.Initializer),
                GetTypeReferences(property.Getter),
                GetTypeReferences(property.Setter),
                Optional(property.ExplicitInterfaceSpecifier)),
            FieldDeclaration field => Concat([field.Type], GetTypeReferences(field.Initializer)),
            EventDeclaration @event => Concat(
                [@event.Type],
                GetTypeReferences(@event.Accessors?.Add),
                GetTypeReferences(@event.Accessors?.Remove),
                Optional(@event.ExplicitInterfaceSpecifier)),
            EnumMemberDeclaration enumMember => GetTypeReferences(enumMember.Initializer),
            RawMemberDeclaration raw => GetTypeReferences(raw.Text),
            _ => [],
        };

        foreach (var type in own)
        {
            yield return type;
        }
    }

    /// <summary>
    ///     Gets the reference and every reference inside it: the containing type, the type arguments, the element
    ///     type, the tuple elements, the function pointer signature.
    /// </summary>
    public static IEnumerable<TypeReference> DescendantsAndSelf(this TypeReference type)
    {
        yield return type;

        var inner = type switch
        {
            NamedTypeReference named => Concat(Optional(named.ContainingType), named.TypeArguments),
            ArrayTypeReference array => [array.ElementType],
            PointerTypeReference pointer => [pointer.PointedAtType],
            FunctionPointerTypeReference functionPointer => Concat([functionPointer.ReturnType], ParameterTypes(functionPointer.Parameters)),
            TupleTypeReference tuple => ElementTypes(tuple.Elements),
            _ => [],
        };

        foreach (var reference in inner)
        {
            foreach (var descendant in reference.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<TypeReference> GetTypeReferences(TypeDeclaration type)
    {
        var own = Concat(
            Optional(type.BaseType),
            GetTypeReferences(type.BaseArguments),
            type.Interfaces,
            GetTypeReferences(type.Parameters),
            GetTypeReferences(type.TypeParameters),
            Optional(type.EnumUnderlyingType),
            GetTypeReferences(type.Members),
            GetContainingTypeReferences(type.ContainingType));

        foreach (var reference in own)
        {
            yield return reference;
        }
    }

    /// <summary>
    ///     Gets the type parameter references of each containing type. The `partial` parts written around a nested
    ///     declaration repeat their type parameters.
    /// </summary>
    private static IEnumerable<TypeReference> GetContainingTypeReferences(TypeDeclaration? containingType)
    {
        for (var outer = containingType; outer is not null; outer = outer.ContainingType)
        {
            foreach (var reference in GetTypeReferences(outer.TypeParameters))
            {
                yield return reference;
            }
        }
    }

    private static IEnumerable<TypeReference> GetTypeReferences(EquatableArray<MemberDeclaration> members)
    {
        foreach (var member in members)
        {
            foreach (var type in member.GetTypeReferences())
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<TypeReference> GetTypeReferences(EquatableArray<AttributeSpecification> attributes)
    {
        foreach (var attribute in attributes)
        {
            yield return attribute.Type;
            foreach (var argument in attribute.Arguments)
            {
                foreach (var type in GetTypeReferences(argument))
                {
                    yield return type;
                }
            }
        }
    }

    private static IEnumerable<TypeReference> GetTypeReferences(EquatableArray<ParameterDeclaration> parameters)
    {
        foreach (var parameter in parameters)
        {
            foreach (var type in GetTypeReferences(parameter))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<TypeReference> GetTypeReferences(ParameterDeclaration parameter)
    {
        return Concat(GetTypeReferences(parameter.Attributes), [parameter.Type], GetTypeReferences(parameter.Default));
    }

    private static IEnumerable<TypeReference> GetTypeReferences(EquatableArray<TypeParameterDeclaration> typeParameters)
    {
        foreach (var parameter in typeParameters)
        {
            foreach (var type in Concat(GetTypeReferences(parameter.Attributes), parameter.ConstraintTypes))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<TypeReference> GetTypeReferences(AccessorDeclaration? accessor)
    {
        return accessor is null ? [] : Concat(GetTypeReferences(accessor.Attributes), GetTypeReferences(accessor.Body));
    }

    private static IEnumerable<TypeReference> GetTypeReferences(Snippet? snippet)
    {
        if (snippet is null)
        {
            yield break;
        }

        foreach (var hole in snippet.Holes)
        {
            yield return hole.Type;
        }
    }

    private static IEnumerable<TypeReference> ParameterTypes(EquatableArray<FunctionPointerParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            yield return parameter.Type;
        }
    }

    private static IEnumerable<TypeReference> ElementTypes(EquatableArray<TupleElement> elements)
    {
        foreach (var element in elements)
        {
            yield return element.Type;
        }
    }

    private static IEnumerable<TypeReference> Optional(TypeReference? type)
    {
        if (type is not null)
        {
            yield return type;
        }
    }

    private static IEnumerable<TypeReference> Concat(params IEnumerable<TypeReference>[] parts)
    {
        foreach (var part in parts)
        {
            foreach (var type in part)
            {
                yield return type;
            }
        }
    }
}

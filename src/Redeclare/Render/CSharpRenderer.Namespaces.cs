using System;
using System.Collections.Generic;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    /// <summary>
    ///     The namespaces every type reference in the file, holes in snippets included, would need as usings
    ///     under <see cref="Qualification.Minimal"/>, sorted. Types the options write as a keyword are left out.
    ///     The file's own namespace is left in.
    /// </summary>
    public static SortedSet<string> CollectNamespaces(CompilationUnit unit, RenderOptions options)
    {
        SortedSet<string> namespaces = new(StringComparer.Ordinal);
        foreach (var member in unit.Members)
        {
            Collect(member, namespaces, options);
        }

        return namespaces;
    }

    private static void Collect(MemberDeclaration member, SortedSet<string> namespaces, RenderOptions options)
    {
        switch (member)
        {
            case NamespaceDeclaration ns:
            {
                foreach (var inner in ns.Members)
                {
                    Collect(inner, namespaces, options);
                }

                break;
            }
            case TypeDeclaration type:
            {
                Collect(type, namespaces, options);
                break;
            }
            default:
            {
                break;
            }
        }
    }

    private static void Collect(TypeDeclaration type, SortedSet<string> namespaces, RenderOptions options)
    {
        Collect(type.Attributes, namespaces, options);
        Collect(type.BaseType, namespaces, options);
        Collect(type.Interfaces, namespaces, options);
        Collect(type.ParameterList, namespaces, options);
        Collect(type.TypeParameters, namespaces, options);
        Collect(type.EnumUnderlyingType, namespaces, options);
        Collect(type.ReturnType, namespaces, options);
        Collect(type.Members, namespaces, options);

        // Walks the type parameters of each containing type. The renderer writes the containing types as `partial`
        // parts that repeat their type parameters, so their constraints and attributes end up in the file too.
        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            Collect(outer.TypeParameters, namespaces, options);
        }
    }

    private static void Collect(EquatableArray<MemberDeclaration> members, SortedSet<string> namespaces, RenderOptions options)
    {
        foreach (var member in members)
        {
            Collect(member.Attributes, namespaces, options);
            switch (member)
            {
                case TypeDeclaration nested:
                {
                    Collect(nested, namespaces, options);
                    break;
                }
                case ExtensionDeclaration extension:
                {
                    Collect([extension.Receiver], namespaces, options);
                    Collect(extension.TypeParameters, namespaces, options);
                    Collect(extension.Members, namespaces, options);
                    break;
                }
                case MethodDeclaration method:
                {
                    Collect(method.ReturnType, namespaces, options);
                    Collect(method.Parameters, namespaces, options);
                    Collect(method.TypeParameters, namespaces, options);
                    Collect(method.Body, namespaces, options);
                    Collect(method.ExplicitInterfaceSpecifier, namespaces, options);
                    break;
                }
                case ConstructorDeclaration constructor:
                {
                    Collect(constructor.Parameters, namespaces, options);
                    Collect(constructor.Body, namespaces, options);
                    Collect(constructor.Initializer, namespaces, options);
                    break;
                }
                case PropertyDeclaration property:
                {
                    Collect(property.Type, namespaces, options);
                    Collect(property.Parameters, namespaces, options);
                    Collect(property.Initializer, namespaces, options);
                    Collect(property.Getter?.Body, namespaces, options);
                    Collect(property.Setter?.Body, namespaces, options);
                    Collect(property.ExplicitInterfaceSpecifier, namespaces, options);
                    break;
                }
                case FieldDeclaration field:
                {
                    Collect(field.Type, namespaces, options);
                    Collect(field.Initializer, namespaces, options);
                    break;
                }
                case EventDeclaration @event:
                {
                    Collect(@event.Type, namespaces, options);
                    Collect(@event.Adder?.Body, namespaces, options);
                    Collect(@event.Remover?.Body, namespaces, options);
                    Collect(@event.ExplicitInterfaceSpecifier, namespaces, options);
                    break;
                }
                case EnumMemberDeclaration enumMember:
                {
                    Collect(enumMember.Value, namespaces, options);
                    break;
                }
                case RawMemberDeclaration raw:
                {
                    Collect(raw.Text, namespaces, options);
                    break;
                }
                default:
                {
                    break;
                }
            }
        }
    }

    private static void Collect(EquatableArray<AttributeSpecification> attributes, SortedSet<string> namespaces, RenderOptions options)
    {
        foreach (var attribute in attributes)
        {
            Collect(attribute.Type, namespaces, options);
            foreach (var argument in attribute.Arguments)
            {
                Collect(argument, namespaces, options);
            }
        }
    }

    private static void Collect(EquatableArray<ParameterDeclaration> parameters, SortedSet<string> namespaces, RenderOptions options)
    {
        foreach (var parameter in parameters)
        {
            Collect(parameter.Attributes, namespaces, options);
            Collect(parameter.Type, namespaces, options);
            Collect(parameter.Default, namespaces, options);
        }
    }

    private static void Collect(EquatableArray<TypeParameterDeclaration> typeParameters, SortedSet<string> namespaces, RenderOptions options)
    {
        foreach (var parameter in typeParameters)
        {
            Collect(parameter.Attributes, namespaces, options);
            Collect(parameter.ConstraintTypes, namespaces, options);
        }
    }

    private static void Collect(Snippet? snippet, SortedSet<string> namespaces, RenderOptions options)
    {
        if (snippet is null)
        {
            return;
        }

        foreach (var hole in snippet.Holes)
        {
            Collect(hole.Type, namespaces, options);
        }
    }

    private static void Collect(EquatableArray<TypeReference> types, SortedSet<string> namespaces, RenderOptions options)
    {
        foreach (var type in types)
        {
            Collect(type, namespaces, options);
        }
    }

    private static void Collect(TypeReference? type, SortedSet<string> namespaces, RenderOptions options)
    {
        switch (type)
        {
            case NamedTypeReference named:
            {
                if (named.ContainingType is { } containing)
                {
                    Collect(containing, namespaces, options);
                }
                else if (named.ContainingNamespace is { Length: > 0 } ns && !WritesKeyword(named, options))
                {
                    namespaces.Add(ns);
                }

                Collect(named.TypeArguments, namespaces, options);
                break;
            }
            case ArrayTypeReference array:
            {
                Collect(array.ElementType, namespaces, options);
                break;
            }
            case PointerTypeReference pointer:
            {
                Collect(pointer.PointedAtType, namespaces, options);
                break;
            }
            case FunctionPointerTypeReference functionPointer:
            {
                Collect(functionPointer.ReturnType, namespaces, options);
                foreach (var parameter in functionPointer.Parameters)
                {
                    Collect(parameter.Type, namespaces, options);
                }

                break;
            }
            case TupleTypeReference tuple:
            {
                foreach (var element in tuple.Elements)
                {
                    Collect(element.Type, namespaces, options);
                }

                break;
            }
            default:
            {
                break;
            }
        }
    }
}

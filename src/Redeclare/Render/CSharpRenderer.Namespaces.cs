using System;
using System.Collections.Generic;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    /// <summary>
    ///     The namespaces every type reference in the file, holes in snippets included, would need as usings
    ///     under <see cref="Qualification.Minimal"/>, sorted. Types with a keyword are left out. The file's own
    ///     namespace is left in.
    /// </summary>
    public static SortedSet<string> CollectNamespaces(CompilationUnit unit)
    {
        SortedSet<string> namespaces = new(StringComparer.Ordinal);
        foreach (var member in unit.Members)
        {
            Collect(member, namespaces);
        }

        return namespaces;
    }

    private static void Collect(MemberDeclaration member, SortedSet<string> namespaces)
    {
        switch (member)
        {
            case NamespaceDeclaration ns:
            {
                foreach (var inner in ns.Members)
                {
                    Collect(inner, namespaces);
                }

                break;
            }
            case TypeDeclaration type:
            {
                Collect(type, namespaces);
                break;
            }
            default:
            {
                break;
            }
        }
    }

    private static void Collect(TypeDeclaration type, SortedSet<string> namespaces)
    {
        Collect(type.Attributes, namespaces);
        Collect(type.BaseType, namespaces);
        Collect(type.Interfaces, namespaces);
        Collect(type.ParameterList, namespaces);
        Collect(type.TypeParameters, namespaces);
        Collect(type.EnumUnderlyingType, namespaces);
        Collect(type.ReturnType, namespaces);

        foreach (var member in type.Members)
        {
            Collect(member.Attributes, namespaces);
            switch (member)
            {
                case TypeDeclaration nested:
                {
                    Collect(nested, namespaces);
                    break;
                }
                case MethodDeclaration method:
                {
                    Collect(method.ReturnType, namespaces);
                    Collect(method.Parameters, namespaces);
                    Collect(method.TypeParameters, namespaces);
                    Collect(method.Body, namespaces);
                    Collect(method.ExplicitInterfaceSpecifier, namespaces);
                    break;
                }
                case ConstructorDeclaration constructor:
                {
                    Collect(constructor.Parameters, namespaces);
                    Collect(constructor.Body, namespaces);
                    Collect(constructor.Initializer, namespaces);
                    break;
                }
                case PropertyDeclaration property:
                {
                    Collect(property.Type, namespaces);
                    Collect(property.Parameters, namespaces);
                    Collect(property.Initializer, namespaces);
                    Collect(property.Getter?.Body, namespaces);
                    Collect(property.Setter?.Body, namespaces);
                    Collect(property.ExplicitInterfaceSpecifier, namespaces);
                    break;
                }
                case FieldDeclaration field:
                {
                    Collect(field.Type, namespaces);
                    Collect(field.Initializer, namespaces);
                    break;
                }
                case EventDeclaration @event:
                {
                    Collect(@event.Type, namespaces);
                    Collect(@event.Adder?.Body, namespaces);
                    Collect(@event.Remover?.Body, namespaces);
                    break;
                }
                case EnumMemberDeclaration enumMember:
                {
                    Collect(enumMember.Value, namespaces);
                    break;
                }
                case RawMemberDeclaration raw:
                {
                    Collect(raw.Text, namespaces);
                    break;
                }
                default:
                {
                    break;
                }
            }
        }
    }

    private static void Collect(EquatableArray<AttributeSpecification> attributes, SortedSet<string> namespaces)
    {
        foreach (var attribute in attributes)
        {
            Collect(attribute.Type, namespaces);
            foreach (var argument in attribute.Arguments)
            {
                Collect(argument, namespaces);
            }
        }
    }

    private static void Collect(EquatableArray<ParameterDeclaration> parameters, SortedSet<string> namespaces)
    {
        foreach (var parameter in parameters)
        {
            Collect(parameter.Attributes, namespaces);
            Collect(parameter.Type, namespaces);
            Collect(parameter.Default, namespaces);
        }
    }

    private static void Collect(EquatableArray<TypeParameterDeclaration> typeParameters, SortedSet<string> namespaces)
    {
        foreach (var parameter in typeParameters)
        {
            Collect(parameter.Attributes, namespaces);
            Collect(parameter.ConstraintTypes, namespaces);
        }
    }

    private static void Collect(Snippet? snippet, SortedSet<string> namespaces)
    {
        if (snippet is null)
        {
            return;
        }

        foreach (var hole in snippet.Holes)
        {
            Collect(hole.Type, namespaces);
        }
    }

    private static void Collect(EquatableArray<TypeReference> types, SortedSet<string> namespaces)
    {
        foreach (var type in types)
        {
            Collect(type, namespaces);
        }
    }

    private static void Collect(TypeReference? type, SortedSet<string> namespaces)
    {
        switch (type)
        {
            case NamedTypeReference named:
            {
                if (named.ContainingType is { } containing)
                {
                    Collect(containing, namespaces);
                }
                else if (named.ContainingNamespace is { Length: > 0 } ns && Keyword(named) is null)
                {
                    namespaces.Add(ns);
                }

                Collect(named.TypeArguments, namespaces);
                break;
            }
            case ArrayTypeReference array:
            {
                Collect(array.ElementType, namespaces);
                break;
            }
            case PointerTypeReference pointer:
            {
                Collect(pointer.PointedAtType, namespaces);
                break;
            }
            case FunctionPointerTypeReference functionPointer:
            {
                Collect(functionPointer.ReturnType, namespaces);
                foreach (var parameter in functionPointer.Parameters)
                {
                    Collect(parameter.Type, namespaces);
                }

                break;
            }
            case TupleTypeReference tuple:
            {
                foreach (var element in tuple.Elements)
                {
                    Collect(element.Type, namespaces);
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

using Microsoft.CodeAnalysis;
using System.Text;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    /// <summary>
    ///     Renders a whole file.
    /// </summary>
    public static string Render(CompilationUnit unit, RenderOptions options)
    {
        options = options.Apply(unit.Overrides);
        SourceWriter writer = new(options);

        if (unit.Header is { } header)
        {
            writer.WriteSnippet(header, options);
        }

        if (!unit.Usings.IsEmpty)
        {
            writer.BlankLine();
            foreach (var directive in unit.Usings)
            {
                writer.BeginLine().Append("using ").Append(directive).Append(';');
                writer.EndLine();
            }
        }

        writer.BlankLine();

        // A file-scoped namespace must be the only member of its file. A namespace with no name is the global
        // one and writes no line at all.
        bool fileScoped = unit.Members is { Length: 1 }
            && unit.Members[0] is NamespaceDeclaration { Name.Length: > 0 }
            && options.NamespaceDeclarations == NamespaceDeclarationPreference.FileScoped
            && options.Allows(CSharpVersion.CSharp10);

        RenderTopLevel(writer, unit.Members, options, fileScoped);

        return writer.ToString();
    }

    /// <summary>
    ///     Renders a type declaration and its members.
    /// </summary>
    public static void Render(SourceWriter writer, TypeDeclaration type, RenderOptions options)
    {
        // A type that says what it is declared in is written inside a partial part per level, outermost first.
        // Those parts carry kind, name and type parameters only.
        if (type.ContainingType is { } containing)
        {
            var wrapped = containing with { Members = [type with { ContainingType = null }] };
            Render(writer, PartOf(wrapped), options);
            return;
        }

        options = options.Apply(type.Overrides);
        string what = $"Type '{type.Name}'";

        RenderDocumentation(writer, type.DocumentationComment);
        RenderAttributes(writer, type.Attributes, options);

        var head = writer.BeginLine().AppendModifiers(type.Accessibility, type.Modifiers, options, what);

        if (type.TypeKind == TypeKind.Delegate)
        {
            RenderDelegate(writer, head, type, options, what);
            return;
        }

        if (type.ReturnType is { } stray)
        {
            throw new RenderException($"{what} is a {type.TypeKind} with a return type ({RenderType(stray, options)}). Only a delegate has one.");
        }

        switch (type.TypeKind, type.IsRecord)
        {
            case (TypeKind.Class, false):
            {
                head.Append("class ");
                break;
            }
            case (TypeKind.Class, true):
            {
                Require(options, CSharpVersion.CSharp9, "a record", what);
                head.Append("record ");
                break;
            }
            case (TypeKind.Struct, false):
            {
                head.Append("struct ");
                break;
            }
            case (TypeKind.Struct, true):
            {
                Require(options, CSharpVersion.CSharp10, "a record struct", what);
                head.Append("record struct ");
                break;
            }
            case (TypeKind.Interface, _):
            {
                head.Append("interface ");
                break;
            }
            case (TypeKind.Enum, _):
            {
                head.Append("enum ");
                break;
            }
            default:
            {
                throw new RenderException($"{what} is a {type.TypeKind}, which has no typed declaration. Use RawMemberDeclaration.");
            }
        }

        head.AppendIdentifier(type.Name).AppendTypeParameters(type.TypeParameters, options);

        if (!type.ParameterList.IsEmpty)
        {
            if (!type.IsRecord)
            {
                Require(options, CSharpVersion.CSharp12, "a primary constructor", what);
            }

            head.Append('(').AppendParameters(type.ParameterList, options, what).Append(')');
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            if (type.EnumUnderlyingType is { } underlying)
            {
                head.Append(" : ").AppendType(underlying, options);
            }
        }
        else if (type.BaseType is { } baseType)
        {
            head.Append(" : ").AppendType(baseType, options);
            if (!type.Interfaces.IsEmpty)
            {
                head.Append(", ").AppendTypes(type.Interfaces, options);
            }
        }
        else if (!type.Interfaces.IsEmpty)
        {
            head.Append(" : ").AppendTypes(type.Interfaces, options);
        }

        head.AppendConstraints(type.TypeParameters, options, what);
        writer.EndLine();

        using (writer.Block())
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                RenderEnumMembers(writer, type, options);
            }
            else
            {
                RenderMembers(writer, type, options);
            }
        }
    }

    /// <summary>
    ///     Renders one member inside <paramref name="containing"/>.
    /// </summary>
    public static void Render(SourceWriter writer, MemberDeclaration member, TypeDeclaration containing, RenderOptions options)
    {
        options = options.Apply(member.Overrides);

        switch (member)
        {
            case TypeDeclaration nested:
            {
                Render(writer, nested, options);
                break;
            }
            case NamespaceDeclaration ns:
            {
                throw new RenderException($"Namespace '{ns.Name}' inside '{containing.Name}'. A namespace may only appear in a file or a namespace.");
            }
            case ExtensionDeclaration extension:
            {
                RenderExtension(writer, extension, containing, options);
                break;
            }
            case MethodDeclaration method:
            {
                RenderMethod(writer, method, containing, options);
                break;
            }
            case ConstructorDeclaration constructor:
            {
                RenderConstructor(writer, constructor, containing, options);
                break;
            }
            case PropertyDeclaration property:
            {
                RenderProperty(writer, property, containing, options);
                break;
            }
            case FieldDeclaration field:
            {
                RenderField(writer, field, containing, options);
                break;
            }
            case EventDeclaration @event:
            {
                RenderEvent(writer, @event, containing, options);
                break;
            }
            case EnumMemberDeclaration enumMember:
            {
                RenderEnumMember(writer, enumMember, options);
                break;
            }
            case RawMemberDeclaration raw:
            {
                RenderDocumentation(writer, raw.DocumentationComment);
                RenderAttributes(writer, raw.Attributes, options);
                writer.WriteSnippet(raw.Text, options);
                break;
            }
            default:
            {
                throw new RenderException($"Unknown member declaration {member.GetType().Name}.");
            }
        }
    }

    /// <summary>
    ///     Renders the members of a file or a namespace: namespaces and types, a blank line between them.
    /// </summary>
    private static void RenderTopLevel(SourceWriter writer, EquatableArray<MemberDeclaration> members, RenderOptions options, bool fileScoped)
    {
        for (int i = 0; i < members.Length; i++)
        {
            if (i > 0)
            {
                writer.BlankLine();
            }

            var member = members[i];
            var memberOptions = options.Apply(member.Overrides);
            switch (member)
            {
                case NamespaceDeclaration { Name.Length: 0 } global:
                {
                    RenderTopLevel(writer, global.Members, memberOptions, fileScoped: false);
                    break;
                }
                case NamespaceDeclaration ns when fileScoped:
                {
                    writer.BeginLine().Append("namespace ").AppendQualifiedName(ns.Name).Append(';');
                    writer.EndLine();
                    writer.BlankLine();
                    RenderTopLevel(writer, ns.Members, memberOptions, fileScoped: false);
                    break;
                }
                case NamespaceDeclaration ns:
                {
                    writer.BeginLine().Append("namespace ").AppendQualifiedName(ns.Name);
                    writer.EndLine();
                    using (writer.Block())
                    {
                        RenderTopLevel(writer, ns.Members, memberOptions, fileScoped: false);
                    }

                    break;
                }
                case TypeDeclaration type:
                {
                    Render(writer, type, memberOptions);
                    break;
                }
                default:
                {
                    throw new RenderException(
                        $"A {member.GetType().Name} at the top level of a file or namespace. Only namespaces and types may appear there.");
                }
            }
        }
    }

    /// <summary>
    ///     Renders a delegate, the one type declaration that is a signature and nothing else: it takes the return
    ///     type and parameter list and ends at a semicolon.
    /// </summary>
    private static void RenderDelegate(SourceWriter writer, StringBuilder head, TypeDeclaration type, RenderOptions options, string what)
    {
        if (type.ReturnType is not { } returnType)
        {
            throw new RenderException($"{what} is a delegate without a return type. Set ReturnType, a reference to System.Void for none.");
        }

        if (!type.Members.IsEmpty)
        {
            throw new RenderException($"{what} is a delegate with {type.Members.Length} members. A delegate declares only its signature.");
        }

        if (type.BaseType is not null || !type.Interfaces.IsEmpty)
        {
            throw new RenderException($"{what} is a delegate with a base type or interfaces. A delegate has neither.");
        }

        head.Append("delegate ")
            .Append(RefText(type.RefKind, options, what))
            .AppendType(returnType, options)
            .Append(' ')
            .AppendIdentifier(type.Name)
            .AppendTypeParameters(type.TypeParameters, options)
            .Append('(')
            .AppendParameters(type.ParameterList, options, what)
            .Append(')')
            .AppendConstraints(type.TypeParameters, options, what)
            .Append(';');
        writer.EndLine();
    }

    /// <summary>
    ///     A partial part of a type that declares nothing of its own: what the renderer writes around a
    ///     declaration that names its <see cref="TypeDeclaration.ContainingType"/>.
    /// </summary>
    private static TypeDeclaration PartOf(TypeDeclaration type)
    {
        return type with
        {
            Accessibility = Accessibility.NotApplicable,
            Modifiers = Modifiers.Partial,
            DocumentationComment = null,
            Attributes = default,
            ParameterList = default,
            BaseType = null,
            Interfaces = default,
            EnumUnderlyingType = null,
        };
    }

    private static void RenderDocumentation(SourceWriter writer, string? documentation)
    {
        if (string.IsNullOrWhiteSpace(documentation))
        {
            return;
        }

        foreach (var line in Snippet.Dedent(documentation!))
        {
            writer.WriteLine(line.Length == 0 ? "///" : "/// " + line);
        }
    }

    private static void RenderAttributes(SourceWriter writer, EquatableArray<AttributeSpecification> attributes, RenderOptions options)
    {
        foreach (var attribute in attributes)
        {
            writer.BeginLine().Append('[').AppendAttribute(attribute, options).Append(']');
            writer.EndLine();
        }
    }
}

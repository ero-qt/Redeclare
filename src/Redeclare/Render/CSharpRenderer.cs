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
        SourceWriter writer = new(options);

        if (unit.Header is { } header)
        {
            writer.WriteSnippet(header);
        }

        RenderDirectives(writer, unit);

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

        RenderTopLevel(writer, unit.Members, fileScoped: CanBeFileScoped(unit, options));

        return writer.ToString();
    }

    /// <summary>
    ///     Renders a type declaration and its members. A type that names the type it is declared in is written
    ///     inside one <c>partial</c> part per containing type, outermost first, so that a file holding one nested
    ///     type puts it back where it belongs.
    /// </summary>
    public static void Render(SourceWriter writer, TypeDeclaration type)
    {
        var options = writer.Options;

        if (type.ContainingType is { } containing)
        {
            RenderInside(writer, containing, type);
            return;
        }

        RenderDeclaration(writer, type);
    }

    private static void RenderInside(SourceWriter writer, TypeDeclaration containing, TypeDeclaration type)
    {
        var part = containing with { Members = [type] };
        if (containing.ContainingType is { } outer)
        {
            RenderInside(writer, outer, part);
            return;
        }

        RenderDeclaration(writer, part);
    }

    /// <summary>
    ///     Renders the declaration itself. A member type keeps its <c>ContainingType</c> as a fact about where it
    ///     is declared, and its place in the member list decides where it is written.
    /// </summary>
    private static void RenderDeclaration(SourceWriter writer, TypeDeclaration type)
    {
        var options = writer.Options;

        string what = $"Type '{type.Name}'";

        RenderDocumentation(writer, type.DocumentationComment);
        RenderAttributes(writer, type.Attributes);

        var head = writer.BeginLine().AppendModifiers(type.Accessibility, type.Modifiers);

        if (type.TypeKind == TypeKind.Delegate)
        {
            RenderDelegate(writer, head, type, what);
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
            if (type.BaseArguments is { } arguments)
            {
                head.Append('(').AppendSnippet(arguments, options).Append(')');
            }

            if (!type.Interfaces.IsEmpty)
            {
                head.Append(", ").AppendTypes(type.Interfaces, options);
            }
        }
        else if (!type.Interfaces.IsEmpty)
        {
            head.Append(" : ").AppendTypes(type.Interfaces, options);
        }

        if (type.BaseArguments is not null && type.BaseType is null)
        {
            throw new RenderException($"{what} passes arguments to a base class but names none. Set BaseType.");
        }

        head.AppendConstraints(type.TypeParameters, options, what);
        writer.EndLine();

        using (writer.Block())
        {
            if (type.TypeKind == TypeKind.Enum)
            {
                RenderEnumMembers(writer, type);
            }
            else
            {
                RenderMembers(writer, type);
            }
        }
    }

    /// <summary>
    ///     Renders one member inside <paramref name="containing"/>.
    /// </summary>
    public static void Render(SourceWriter writer, MemberDeclaration member, TypeDeclaration containing)
    {
        var options = writer.Options;

        switch (member)
        {
            case TypeDeclaration nested:
            {
                RenderDeclaration(writer, nested);
                break;
            }
            case NamespaceDeclaration ns:
            {
                throw new RenderException($"Namespace '{ns.Name}' inside '{containing.Name}'. A namespace may only appear in a file or a namespace.");
            }
            case ExtensionDeclaration extension:
            {
                RenderExtension(writer, extension, containing);
                break;
            }
            case MethodDeclaration method:
            {
                RenderMethod(writer, method, containing);
                break;
            }
            case ConstructorDeclaration constructor:
            {
                RenderConstructor(writer, constructor, containing);
                break;
            }
            case PropertyDeclaration property:
            {
                RenderProperty(writer, property, containing);
                break;
            }
            case FieldDeclaration field:
            {
                RenderField(writer, field, containing);
                break;
            }
            case EventDeclaration @event:
            {
                RenderEvent(writer, @event, containing);
                break;
            }
            case EnumMemberDeclaration enumMember:
            {
                RenderEnumMember(writer, enumMember);
                break;
            }
            case RawMemberDeclaration raw:
            {
                RenderDocumentation(writer, raw.DocumentationComment);
                RenderAttributes(writer, raw.Attributes);
                writer.WriteSnippet(raw.Text);
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
    /// <summary>
    ///     Writes the <c>#nullable</c> and <c>#pragma warning disable</c> lines under the header. <c>#nullable</c> is
    ///     C# 8, and below that it is left out.
    /// </summary>
    private static void RenderDirectives(SourceWriter writer, CompilationUnit unit)
    {
        var options = writer.Options;

        bool nullable = unit.NullableContext is { } context && options.Allows(CSharpVersion.CSharp8);
        if (!nullable && unit.DisabledWarnings.IsEmpty)
        {
            return;
        }

        writer.BlankLine();
        if (nullable)
        {
            writer.WriteLine(unit.NullableContext switch
            {
                NullableContextOptions.Disable => "#nullable disable",
                NullableContextOptions.Warnings => "#nullable enable warnings",
                NullableContextOptions.Annotations => "#nullable enable annotations",
                _ => "#nullable enable",
            });
        }

        if (!unit.DisabledWarnings.IsEmpty)
        {
            var line = writer.BeginLine().Append("#pragma warning disable ");
            for (int i = 0; i < unit.DisabledWarnings.Length; i++)
            {
                if (i > 0)
                {
                    line.Append(", ");
                }

                line.Append(unit.DisabledWarnings[i]);
            }

            writer.EndLine();
        }
    }

    private static bool CanBeFileScoped(CompilationUnit unit, RenderOptions options)
    {
        bool preferred = options.NamespaceDeclarations == NamespaceDeclarationPreference.FileScoped && options.Allows(CSharpVersion.CSharp10);

        return preferred
            && unit.Members is { Length: 1 }
            && unit.Members[0] is NamespaceDeclaration { Name.Length: > 0 } sole
            && !HoldsNamespace(sole);
    }

    private static bool HoldsNamespace(NamespaceDeclaration ns)
    {
        foreach (var member in ns.Members)
        {
            if (member is NamespaceDeclaration)
            {
                return true;
            }
        }

        return false;
    }

    private static void RenderTopLevel(SourceWriter writer, EquatableArray<MemberDeclaration> members, bool fileScoped)
    {
        var options = writer.Options;

        for (int i = 0; i < members.Length; i++)
        {
            if (i > 0)
            {
                writer.BlankLine();
            }

            var member = members[i];
            switch (member)
            {
                case NamespaceDeclaration { Name.Length: 0 } global:
                {
                    RenderTopLevel(writer, global.Members, fileScoped: false);
                    break;
                }
                case NamespaceDeclaration ns when fileScoped:
                {
                    writer.BeginLine().Append("namespace ").AppendQualifiedName(ns.Name).Append(';');
                    writer.EndLine();
                    writer.BlankLine();
                    RenderTopLevel(writer, ns.Members, fileScoped: false);
                    break;
                }
                case NamespaceDeclaration ns:
                {
                    writer.BeginLine().Append("namespace ").AppendQualifiedName(ns.Name);
                    writer.EndLine();
                    using (writer.Block())
                    {
                        RenderTopLevel(writer, ns.Members, fileScoped: false);
                    }

                    break;
                }
                case TypeDeclaration type:
                {
                    Render(writer, type);
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
    private static void RenderDelegate(SourceWriter writer, StringBuilder head, TypeDeclaration type, string what)
    {
        var options = writer.Options;

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
            .Append(RefText(type.RefKind, what))
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

    private static void RenderAttributes(SourceWriter writer, EquatableArray<AttributeSpecification> attributes)
    {
        var options = writer.Options;

        foreach (var attribute in attributes)
        {
            writer.BeginLine().Append('[').AppendAttribute(attribute, options).Append(']');
            writer.EndLine();
        }
    }
}

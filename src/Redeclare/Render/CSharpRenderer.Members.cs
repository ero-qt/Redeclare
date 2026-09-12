using Microsoft.CodeAnalysis;
using System.Text;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    private static void RenderMembers(SourceWriter writer, TypeDeclaration type, RenderOptions options)
    {
        for (int i = 0; i < type.Members.Length; i++)
        {
            if (i > 0 && options.BlankLineBetweenMembers)
            {
                writer.BlankLine();
            }

            Render(writer, type.Members[i], type, options);
        }
    }

    /// <summary>
    ///     Renders an extension block: <c>extension&lt;T&gt;(Receiver receiver) where ... { members }</c>. Only a
    ///     non-generic static class may hold one, and it holds only methods and properties.
    /// </summary>
    private static void RenderExtension(SourceWriter writer, ExtensionDeclaration extension, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Extension block for '{RenderType(extension.Receiver.Type, options)}' in '{containing.Name}'";
        if (containing.TypeKind != TypeKind.Class || (containing.Modifiers & Modifiers.Static) == 0 || !containing.TypeParameters.IsEmpty)
        {
            throw new RenderException($"{what}. An extension block may only appear in a non-generic static class.");
        }

        foreach (var member in extension.Members)
        {
            if (member is not (MethodDeclaration or PropertyDeclaration or RawMemberDeclaration))
            {
                throw new RenderException($"{what} holds a {member.GetType().Name}. An extension block holds methods and properties only.");
            }
        }

        var receiver = extension.Receiver;
        var head = writer.BeginLine().Append("extension").AppendTypeParameters(extension.TypeParameters, options).Append('(');
        foreach (var attribute in receiver.Attributes)
        {
            head.Append('[').AppendAttribute(attribute, options).Append("] ");
        }

        if (receiver.IsScoped)
        {
            head.Append("scoped ");
        }

        head.Append(ParameterRefText(receiver.RefKind, options, what)).AppendType(receiver.Type, options);
        if (receiver.Name.Length > 0)
        {
            head.Append(' ').AppendIdentifier(receiver.Name);
        }

        head.Append(')').AppendConstraints(extension.TypeParameters, options, what);
        writer.EndLine();

        using (writer.Block())
        {
            for (int i = 0; i < extension.Members.Length; i++)
            {
                if (i > 0 && options.BlankLineBetweenMembers)
                {
                    writer.BlankLine();
                }

                Render(writer, extension.Members[i], containing, options);
            }
        }
    }

    private static void RenderEnumMembers(SourceWriter writer, TypeDeclaration type, RenderOptions options)
    {
        foreach (var member in type.Members)
        {
            if (member is not EnumMemberDeclaration enumMember)
            {
                throw new RenderException($"Enum '{type.Name}' holds a {member.GetType().Name}. An enum may only hold enum members.");
            }

            RenderEnumMember(writer, enumMember, options);
        }
    }

    private static void RenderEnumMember(SourceWriter writer, EnumMemberDeclaration member, RenderOptions options)
    {
        RenderDocumentation(writer, member.DocumentationComment);
        RenderAttributes(writer, member.Attributes, options);

        writer.BeginLine().AppendIdentifier(member.Name);
        if (member.Value is { } value)
        {
            writer.BeginLine().Append(" = ");
            writer.WriteInline(value, options);
        }

        writer.BeginLine().Append(',');
        writer.EndLine();
    }

    private static void RenderMethod(SourceWriter writer, MethodDeclaration method, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Method '{containing.Name}.{method.Name}'";

        RenderDocumentation(writer, method.DocumentationComment);
        RenderAttributes(writer, method.Attributes, options);

        var head = writer.BeginLine()
            .AppendModifiers(method.Accessibility, method.Modifiers, options, what)
            .Append(RefText(method.RefKind, options, what));

        // Writes a conversion as `implicit operator Target(`. The target type takes the name's place, and an
        // explicit implementation puts its interface between the keyword and `operator`.
        if (method.Name is "implicit operator" or "explicit operator" or "explicit operator checked")
        {
            int keyword = method.Name.IndexOf(' ');
            head.Append(method.Name, 0, keyword + 1);
            if (method.ExplicitInterfaceSpecifier is { } explicitInterface)
            {
                head.AppendType(explicitInterface, options).Append('.');
            }

            head.Append(method.Name, keyword + 1, method.Name.Length - keyword - 1).Append(' ').AppendType(method.ReturnType, options);
        }
        else
        {
            head.AppendType(method.ReturnType, options).Append(' ');
            if (method.ExplicitInterfaceSpecifier is { } explicitInterface)
            {
                head.AppendType(explicitInterface, options).Append('.');
            }

            head.AppendIdentifier(method.Name);
        }

        head.AppendTypeParameters(method.TypeParameters, options)
            .Append('(')
            .AppendParameters(method.Parameters, options, what)
            .Append(')')
            .AppendConstraints(method.TypeParameters, options, what);

        // Checks whether the body hands a value back. An `async` method returning `Task`, or any task-like type without
        // a type argument, does not.
        bool returnsValue = method.ReturnType is not NamedTypeReference { SpecialType: SpecialType.System_Void }
            && !((method.Modifiers & Modifiers.Async) != 0 && method.ReturnType is NamedTypeReference { Arity: 0 });
        RenderBody(writer, method.Body, options.Methods, CSharpVersion.CSharp6, options, returnsValue, what);
    }

    private static void RenderConstructor(SourceWriter writer, ConstructorDeclaration constructor, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Constructor of '{containing.Name}'";

        RenderDocumentation(writer, constructor.DocumentationComment);
        RenderAttributes(writer, constructor.Attributes, options);

        var head = writer.BeginLine()
            .AppendModifiers(constructor.Accessibility, constructor.Modifiers, options, what)
            .AppendIdentifier(containing.Name)
            .Append('(')
            .AppendParameters(constructor.Parameters, options, what)
            .Append(')');
        if (constructor.Initializer is { } initializer)
        {
            head.Append(" : ").AppendSnippet(initializer, options);
        }

        RenderBody(writer, constructor.Body, options.Constructors, CSharpVersion.CSharp7, options, returnsValue: false, what);
    }

    private static void RenderProperty(SourceWriter writer, PropertyDeclaration property, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Property '{containing.Name}.{property.Name}'";
        var getter = property.Getter;
        var setter = property.Setter;

        if (getter is null && setter is null)
        {
            throw new RenderException($"{what} has neither a getter nor a setter.");
        }

        if (property.RefKind != RefKind.None && setter is not null)
        {
            throw new RenderException($"{what} returns by reference and has a setter. A ref property has a getter only.");
        }

        bool allAuto = getter is null or { Body: null } && setter is null or { Body: null };
        if (!allAuto && property.Initializer is { } trailing)
        {
            throw new RenderException(
                $"{what} has an initializer ({trailing.Render(options)}) but an accessor with a body. Only auto properties take initializers.");
        }

        RenderDocumentation(writer, property.DocumentationComment);
        RenderAttributes(writer, property.Attributes, options);

        var head = writer.BeginLine()
            .AppendModifiers(property.Accessibility, property.Modifiers, options, what)
            .Append(RefText(property.RefKind, options, what))
            .AppendType(property.Type, options)
            .Append(' ');
        if (property.ExplicitInterfaceSpecifier is { } explicitInterface)
        {
            head.AppendType(explicitInterface, options).Append('.');
        }

        if (property.IsIndexer)
        {
            head.Append("this[").AppendParameters(property.Parameters, options, what).Append(']');
        }
        else
        {
            head.AppendIdentifier(property.Name);
        }

        // Writes a getter-only property with an expression getter as `=> expression` when the options prefer that.
        var preference = property.IsIndexer ? options.Indexers : options.Properties;
        if (setter is null && getter is { Body: { IsExpression: true } expression } && Arrow(expression, preference, CSharpVersion.CSharp6, options))
        {
            WriteArrow(writer, expression, options);
            return;
        }

        if (allAuto)
        {
            head.Append(" { ");
            if (getter is not null)
            {
                AppendAccessorHead(head, getter, "get").Append("; ");
            }

            if (setter is not null)
            {
                AppendAccessorHead(head, setter, setter.IsInitOnly ? "init" : "set").Append("; ");
            }

            head.Append('}');
            if (property.Initializer is { } initializer)
            {
                head.Append(" = ");
                writer.WriteInline(initializer, options);
                writer.BeginLine().Append(';');
            }

            writer.EndLine();
            return;
        }

        writer.EndLine();
        using (writer.Block())
        {
            if (getter is not null)
            {
                AppendAccessorHead(writer.BeginLine(), getter, "get");
                RenderBody(writer, getter.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: true, what);
            }

            if (setter is not null)
            {
                AppendAccessorHead(writer.BeginLine(), setter, setter.IsInitOnly ? "init" : "set");
                RenderBody(writer, setter.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: false, what);
            }
        }
    }

    private static void RenderField(SourceWriter writer, FieldDeclaration field, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Field '{containing.Name}.{field.Name}'";

        RenderDocumentation(writer, field.DocumentationComment);
        RenderAttributes(writer, field.Attributes, options);

        writer.BeginLine()
            .AppendModifiers(field.Accessibility, field.Modifiers, options, what)
            .Append(RefText(field.RefKind, options, what))
            .AppendType(field.Type, options)
            .Append(' ')
            .AppendIdentifier(field.Name);
        if (field.Initializer is { } initializer)
        {
            writer.BeginLine().Append(" = ");
            writer.WriteInline(initializer, options);
        }

        writer.BeginLine().Append(';');
        writer.EndLine();
    }

    private static void RenderEvent(SourceWriter writer, EventDeclaration @event, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Event '{containing.Name}.{@event.Name}'";

        bool fieldLike = @event.Adder is null && @event.Remover is null;
        if (!fieldLike && (@event.Adder is null || @event.Remover is null))
        {
            throw new RenderException($"{what} declares one accessor. C# requires an event with accessors to have both add and remove.");
        }

        if (fieldLike && @event.ExplicitInterfaceSpecifier is not null)
        {
            throw new RenderException($"{what} is an explicit implementation. C# requires an explicit implementation to declare both accessors.");
        }

        RenderDocumentation(writer, @event.DocumentationComment);
        RenderAttributes(writer, @event.Attributes, options);

        var head = writer.BeginLine()
            .AppendModifiers(@event.Accessibility, @event.Modifiers, options, what)
            .Append("event ")
            .AppendType(@event.Type, options)
            .Append(' ');
        if (@event.ExplicitInterfaceSpecifier is { } explicitInterface)
        {
            head.AppendType(explicitInterface, options).Append('.');
        }

        head.AppendIdentifier(@event.Name);

        if (fieldLike)
        {
            head.Append(';');
            writer.EndLine();
            return;
        }

        writer.EndLine();
        using (writer.Block())
        {
            AppendAccessorHead(writer.BeginLine(), @event.Adder!, "add");
            RenderBody(writer, @event.Adder!.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: false, what);
            AppendAccessorHead(writer.BeginLine(), @event.Remover!, "remove");
            RenderBody(writer, @event.Remover!.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: false, what);
        }
    }

    private static StringBuilder AppendAccessorHead(StringBuilder text, AccessorDeclaration accessor, string keyword)
    {
        if (accessor.Accessibility != Accessibility.NotApplicable)
        {
            text.Append(AccessibilityText(accessor.Accessibility)).Append(' ');
        }

        return text.Append(keyword);
    }

    /// <summary>
    ///     Finishes a declaration whose head has been begun on the current line with its body in whichever form
    ///     the options pick. An arrow needs <paramref name="arrowSince"/>: C# 6 for methods and properties, C# 7
    ///     for accessors and constructors.
    /// </summary>
    private static void RenderBody(
        SourceWriter writer,
        Snippet? body,
        ExpressionBodyPreference preference,
        CSharpVersion arrowSince,
        RenderOptions options,
        bool returnsValue,
        string what)
    {
        if (body is { IsExpression: true } expression)
        {
            // Catches an expression body with nothing in it, which could only render as a dangling `=>`. Whether the
            // text is really one expression is for the compiler to say.
            if (expression.IsEmpty)
            {
                throw new RenderException($"{what} has an expression body with no expression. Use Snippet.Empty as a block body for an empty one.");
            }

            if (Arrow(expression, preference, arrowSince, options))
            {
                WriteArrow(writer, expression, options);
                return;
            }

            // Writes the expression as a block, since the options want one. A `return` goes in front when there is a value.
            writer.EndLine();
            using (writer.Block())
            {
                var lines = expression.Lines;
                for (int i = 0; i < lines.Length; i++)
                {
                    // Keeps an empty line empty. Dedent trims the blank edges, so only a line in the middle can be empty here.
                    if (lines[i].Length == 0)
                    {
                        writer.EndLine();
                        continue;
                    }

                    var text = writer.BeginLine();
                    if (i == 0 && returnsValue)
                    {
                        text.Append("return ");
                    }

                    text.AppendSnippetLine(lines[i], expression.Holes, options);
                    if (i == lines.Length - 1)
                    {
                        text.Append(';');
                    }

                    writer.EndLine();
                }
            }

            return;
        }

        if (body is not null)
        {
            writer.EndLine();
            using (writer.Block())
            {
                writer.WriteSnippet(body, options);
            }

            return;
        }

        writer.BeginLine().Append(';');
        writer.EndLine();
    }

    private static bool Arrow(Snippet expression, ExpressionBodyPreference preference, CSharpVersion since, RenderOptions options)
    {
        if (!options.Allows(since))
        {
            return false;
        }

        return preference switch
        {
            ExpressionBodyPreference.WhenPossible => true,
            ExpressionBodyPreference.WhenOnSingleLine => expression.IsSingleLine,
            _ => false,
        };
    }

    /// <summary>
    ///     Finishes the current line with <c>=&gt; expression;</c>. A multi-line expression continues indented one
    ///     level under the head.
    /// </summary>
    private static void WriteArrow(SourceWriter writer, Snippet expression, RenderOptions options)
    {
        var lines = expression.Lines;
        if (lines.Length == 1)
        {
            writer.BeginLine().Append(" => ").AppendSnippetLine(lines[0], expression.Holes, options).Append(';');
            writer.EndLine();
            return;
        }

        writer.BeginLine().Append(" =>");
        writer.EndLine();
        writer.Indent();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
            {
                writer.EndLine();
                continue;
            }

            var text = writer.BeginLine().AppendSnippetLine(lines[i], expression.Holes, options);
            if (i == lines.Length - 1)
            {
                text.Append(';');
            }

            writer.EndLine();
        }

        writer.Unindent();
    }
}

using Microsoft.CodeAnalysis;
using System;
using System.Globalization;
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
    ///     non-generic static class may hold one, and it holds methods, operators, properties and raw text.
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
                throw new RenderException($"{what} holds a {member.GetType().Name}. An extension block holds methods, operators and properties only.");
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
            .AppendModifiers(method.Accessibility, method.Modifiers)
            .Append(RefText(method.RefKind, options, what));

        switch (method.Name)
        {
            case MethodName.Conversion conversion:
            {
                head.Append(conversion.IsImplicit ? "implicit " : "explicit ")
                    .AppendExplicitInterface(method.ExplicitInterfaceSpecifier, options)
                    .Append(conversion.IsChecked ? "operator checked " : "operator ")
                    .AppendType(method.ReturnType, options);
                break;
            }
            case MethodName.Destructor:
            {
                head.Append('~').AppendIdentifier(containing.Name);
                break;
            }
            case MethodName.Operator @operator:
            {
                head.AppendType(method.ReturnType, options)
                    .Append(' ')
                    .AppendExplicitInterface(method.ExplicitInterfaceSpecifier, options)
                    .Append(@operator.ToString());
                break;
            }
            case MethodName.Ordinary ordinary:
            {
                head.AppendType(method.ReturnType, options)
                    .Append(' ')
                    .AppendExplicitInterface(method.ExplicitInterfaceSpecifier, options)
                    .AppendIdentifier(ordinary.Name);
                break;
            }
        }

        head.AppendTypeParameters(method.TypeParameters, options)
            .Append('(')
            .AppendParameters(method.Parameters, options, what)
            .Append(')')
            .AppendConstraints(method.TypeParameters, options, what);

        RenderBody(writer, method.Body, options.Methods, CSharpVersion.CSharp6, options, ReturnsValue(method), what);
    }

    private static StringBuilder AppendExplicitInterface(this StringBuilder text, TypeReference? explicitInterface, RenderOptions options)
    {
        return explicitInterface is null ? text : text.AppendType(explicitInterface, options).Append('.');
    }

    private static bool ReturnsValue(MethodDeclaration method)
    {
        bool isVoid = method.ReturnType is NamedTypeReference { SpecialType: SpecialType.System_Void };
        bool isAsyncTask = (method.Modifiers & Modifiers.Async) != 0 && method.ReturnType is NamedTypeReference { Arity: 0 };

        return !isVoid && !isAsyncTask;
    }

    private static void RenderConstructor(SourceWriter writer, ConstructorDeclaration constructor, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Constructor of '{containing.Name}'";

        RenderDocumentation(writer, constructor.DocumentationComment);
        RenderAttributes(writer, constructor.Attributes, options);

        var head = writer.BeginLine()
            .AppendModifiers(constructor.Accessibility, constructor.Modifiers)
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
            .AppendModifiers(property.Accessibility, property.Modifiers)
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
                AppendAccessorHead(head, getter, "get", options).Append("; ");
            }

            if (setter is not null)
            {
                AppendAccessorHead(head, setter, setter.IsInitOnly ? "init" : "set", options).Append("; ");
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
                AppendAccessorHead(writer.BeginLine(), getter, "get", options);
                RenderBody(writer, getter.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: true, what);
            }

            if (setter is not null)
            {
                AppendAccessorHead(writer.BeginLine(), setter, setter.IsInitOnly ? "init" : "set", options);
                RenderBody(writer, setter.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: false, what);
            }
        }
    }

    private static void RenderField(SourceWriter writer, FieldDeclaration field, TypeDeclaration containing, RenderOptions options)
    {
        string what = $"Field '{containing.Name}.{field.Name}'";

        RenderDocumentation(writer, field.DocumentationComment);
        RenderAttributes(writer, field.Attributes, options);

        var head = writer.BeginLine()
            .AppendModifiers(field.Accessibility, field.Modifiers)
            .Append(RefText(field.RefKind, options, what));
        if (field.FixedSize > 0)
        {
            head.Append("fixed ");
        }

        head.AppendType(field.Type, options)
            .Append(' ')
            .AppendIdentifier(field.Name);
        if (field.FixedSize > 0)
        {
            head.Append('[').Append(field.FixedSize.ToString(CultureInfo.InvariantCulture)).Append(']');
        }

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

        if (@event.Accessors is null && @event.ExplicitInterfaceSpecifier is not null)
        {
            throw new RenderException($"{what} is an explicit implementation. C# requires an explicit implementation to declare both accessors.");
        }

        RenderDocumentation(writer, @event.DocumentationComment);
        RenderAttributes(writer, @event.Attributes, options);

        var head = writer.BeginLine()
            .AppendModifiers(@event.Accessibility, @event.Modifiers)
            .Append("event ")
            .AppendType(@event.Type, options)
            .Append(' ');
        if (@event.ExplicitInterfaceSpecifier is { } explicitInterface)
        {
            head.AppendType(explicitInterface, options).Append('.');
        }

        head.AppendIdentifier(@event.Name);

        if (@event.Accessors is not { } accessors)
        {
            head.Append(';');
            writer.EndLine();
            return;
        }

        writer.EndLine();
        using (writer.Block())
        {
            AppendAccessorHead(writer.BeginLine(), accessors.Add, "add", options);
            RenderBody(writer, accessors.Add.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: false, what);
            AppendAccessorHead(writer.BeginLine(), accessors.Remove, "remove", options);
            RenderBody(writer, accessors.Remove.Body, options.Accessors, CSharpVersion.CSharp7, options, returnsValue: false, what);
        }
    }

    private static StringBuilder AppendAccessorHead(StringBuilder text, AccessorDeclaration accessor, string keyword, RenderOptions options)
    {
        foreach (var attribute in accessor.Attributes)
        {
            text.Append('[').AppendAttribute(attribute, options).Append("] ");
        }

        if (accessor.Accessibility != Accessibility.NotApplicable)
        {
            text.Append(AccessibilityText(accessor.Accessibility)).Append(' ');
        }

        if (accessor.IsReadOnly)
        {
            text.Append("readonly ");
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
            if (expression.IsEmpty)
            {
                throw new RenderException($"{what} has an expression body with no expression. Use Snippet.Empty as a block body for an empty one.");
            }

            if (Arrow(expression, preference, arrowSince, options))
            {
                WriteArrow(writer, expression, options);
                return;
            }

            bool returns = returnsValue && !IsThrow(expression);
            writer.EndLine();
            using (writer.Block())
            {
                var lines = expression.Lines;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Length == 0)
                    {
                        writer.EndLine();
                        continue;
                    }

                    var text = writer.BeginLine();
                    if (i == 0 && returns)
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

    /// <summary>
    ///     Checks whether the expression is a <c>throw</c>. The first line starts with the keyword and a space.
    /// </summary>
    private static bool IsThrow(Snippet expression)
    {
        string first = expression.Lines[0].TrimStart();

        return first.StartsWith("throw ", StringComparison.Ordinal);
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

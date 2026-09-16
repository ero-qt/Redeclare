using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;

namespace Redeclare;

/// <summary>
///     Provides methods that render the model to C# text under <see cref="RenderOptions"/>.
/// </summary>
internal static partial class CSharpRenderer
{
    /// <summary>
    ///     Appends a name as C# must spell it. A symbol's name is bare text, so a type, member or parameter called
    ///     <c>class</c> or <c>event</c> takes the <c>@</c> that makes it an identifier again.
    /// </summary>
    private static StringBuilder AppendIdentifier(this StringBuilder text, string name)
    {
        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
        {
            text.Append('@');
        }

        return text.Append(name);
    }

    /// <summary>
    ///     Appends a dotted name with every part spelled the way <see cref="AppendIdentifier"/> spells one.
    /// </summary>
    private static StringBuilder AppendQualifiedName(this StringBuilder text, string name)
    {
        int start = 0;
        while (true)
        {
            int dot = name.IndexOf('.', start);
            if (dot < 0)
            {
                return text.AppendIdentifier(start == 0 ? name : name.Substring(start));
            }

            text.AppendIdentifier(name.Substring(start, dot - start)).Append('.');
            start = dot + 1;
        }
    }

    /// <summary>
    ///     Appends the accessibility and modifiers, each followed by a space, in the order the compiler and the
    ///     style guidelines expect.
    /// </summary>
    private static StringBuilder AppendModifiers(this StringBuilder text, Accessibility accessibility, Modifiers modifiers)
    {
        if (accessibility != Accessibility.NotApplicable)
        {
            text.Append(AccessibilityText(accessibility)).Append(' ');
        }

        append(Modifiers.File, "file");
        append(Modifiers.Const, "const");
        append(Modifiers.Static, "static");
        append(Modifiers.Extern, "extern");
        append(Modifiers.New, "new");
        append(Modifiers.Virtual, "virtual");
        append(Modifiers.Abstract, "abstract");
        append(Modifiers.Sealed, "sealed");
        append(Modifiers.Override, "override");
        append(Modifiers.ReadOnly, "readonly");
        append(Modifiers.Unsafe, "unsafe");
        append(Modifiers.Required, "required");
        append(Modifiers.Volatile, "volatile");
        append(Modifiers.Async, "async");
        append(Modifiers.Ref, "ref");
        append(Modifiers.Partial, "partial");

        return text;

        void append(Modifiers flag, string keyword)
        {
            if ((modifiers & flag) != 0)
            {
                text.Append(keyword).Append(' ');
            }
        }
    }

    private static string AccessibilityText(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.NotApplicable => "",
            Accessibility.Private => "private",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.Public => "public",
            _ => throw new RenderException($"Unknown accessibility {accessibility}."),
        };
    }

    /// <summary>
    ///     Gets the <c>ref</c> or <c>ref readonly</c> a return, property or field carries, with the space after it.
    ///     <c>RefKind.RefReadOnly</c> and <c>RefKind.In</c> are the same value. On a return it means
    ///     <c>ref readonly</c>.
    /// </summary>
    private static string RefText(RefKind refKind, string what)
    {
        switch (refKind)
        {
            case RefKind.None:
            {
                return "";
            }
            case RefKind.Ref:
            {
                return "ref ";
            }
            case RefKind.RefReadOnly:
            {
                return "ref readonly ";
            }
            default:
            {
                throw new RenderException($"{what} has ref kind {refKind}. A return, property or field may only be ref or ref readonly.");
            }
        }
    }

    /// <summary>
    ///     Gets the <c>ref</c>, <c>out</c>, <c>in</c> or <c>ref readonly</c> a parameter carries, with the space after
    ///     it.
    /// </summary>
    private static string ParameterRefText(RefKind refKind, string what)
    {
        switch (refKind)
        {
            case RefKind.None:
            {
                return "";
            }
            case RefKind.Ref:
            {
                return "ref ";
            }
            case RefKind.Out:
            {
                return "out ";
            }
            case RefKind.In:
            {
                return "in ";
            }
            case RefKind.RefReadOnlyParameter:
            {
                return "ref readonly ";
            }
            default:
            {
                throw new RenderException($"{what} has a parameter with unknown ref kind {refKind}.");
            }
        }
    }

    /// <summary>
    ///     Appends an attribute without its brackets: <c>target: Type(arguments)</c>.
    /// </summary>
    private static StringBuilder AppendAttribute(this StringBuilder text, AttributeSpecification attribute, RenderOptions options)
    {
        if (attribute.Target is { } target)
        {
            text.Append(target).Append(": ");
        }

        text.AppendType(attribute.Type, options);
        if (!attribute.Arguments.IsEmpty)
        {
            text.Append('(').AppendSnippets(attribute.Arguments, options).Append(')');
        }

        return text;
    }

    /// <summary>
    ///     Appends a parameter list without its parentheses.
    /// </summary>
    private static StringBuilder AppendParameters(
        this StringBuilder text,
        EquatableArray<ParameterDeclaration> parameters,
        RenderOptions options,
        string what)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            var parameter = parameters[i];
            foreach (var attribute in parameter.Attributes)
            {
                text.Append('[').AppendAttribute(attribute, options).Append("] ");
            }

            if (parameter.IsExtensionReceiver)
            {
                text.Append("this ");
            }

            if (parameter.IsScoped)
            {
                text.Append("scoped ");
            }

            text.Append(ParameterRefText(parameter.RefKind, what));

            if (parameter.IsParams)
            {
                text.Append("params ");
            }

            text.AppendType(parameter.Type, options).Append(' ').AppendIdentifier(parameter.Name);
            if (parameter.Default is { } defaultValue)
            {
                text.Append(" = ").AppendSnippet(defaultValue, options);
            }
        }

        return text;
    }

    /// <summary>
    ///     Appends the type parameter list, angle brackets included, or nothing when there are none.
    /// </summary>
    private static StringBuilder AppendTypeParameters(
        this StringBuilder text,
        EquatableArray<TypeParameterDeclaration> typeParameters,
        RenderOptions options)
    {
        if (typeParameters.IsEmpty)
        {
            return text;
        }

        text.Append('<');
        for (int i = 0; i < typeParameters.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            var parameter = typeParameters[i];
            foreach (var attribute in parameter.Attributes)
            {
                text.Append('[').AppendAttribute(attribute, options).Append("] ");
            }

            switch (parameter.Variance)
            {
                case VarianceKind.In:
                {
                    text.Append("in ");
                    break;
                }
                case VarianceKind.Out:
                {
                    text.Append("out ");
                    break;
                }
                default:
                {
                    break;
                }
            }

            text.AppendIdentifier(parameter.Name);
        }

        return text.Append('>');
    }

    /// <summary>
    ///     Appends the <c>where</c> clauses, constraints in grammar order: primary, types, <c>new()</c>,
    ///     <c>allows ref struct</c>.
    /// </summary>
    private static StringBuilder AppendConstraints(
        this StringBuilder text,
        EquatableArray<TypeParameterDeclaration> typeParameters,
        RenderOptions options,
        string what)
    {
        foreach (var parameter in typeParameters)
        {
            if (!parameter.HasConstraints)
            {
                continue;
            }

            text.Append(" where ").AppendIdentifier(parameter.Name).Append(" : ");
            bool first = true;

            if (parameter.HasDefaultConstraint)
            {
                separate().Append("default");
            }

            if (parameter.HasReferenceTypeConstraint)
            {
                bool annotated = parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    && options.NullableAnnotations
                    && options.Allows(CSharpVersion.CSharp8);
                separate().Append(annotated ? "class?" : "class");
            }

            if (parameter.HasUnmanagedTypeConstraint)
            {
                separate().Append("unmanaged");
            }
            else if (parameter.HasValueTypeConstraint)
            {
                separate().Append("struct");
            }

            if (parameter.HasNotNullConstraint)
            {
                separate().Append("notnull");
            }

            foreach (var constraintType in parameter.ConstraintTypes)
            {
                separate().AppendType(constraintType, options);
            }

            if (parameter.HasConstructorConstraint)
            {
                separate().Append("new()");
            }

            if (parameter.AllowsRefLikeType)
            {
                separate().Append("allows ref struct");
            }

            StringBuilder separate()
            {
                if (!first)
                {
                    text.Append(", ");
                }

                first = false;
                return text;
            }
        }

        return text;
    }

    /// <summary>
    ///     Appends type references separated by <c>, </c>.
    /// </summary>
    private static StringBuilder AppendTypes(this StringBuilder text, EquatableArray<TypeReference> types, RenderOptions options)
    {
        for (int i = 0; i < types.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            text.AppendType(types[i], options);
        }

        return text;
    }

    /// <summary>
    ///     Appends snippets separated by <c>, </c>.
    /// </summary>
    private static StringBuilder AppendSnippets(this StringBuilder text, EquatableArray<Snippet> snippets, RenderOptions options)
    {
        for (int i = 0; i < snippets.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            text.AppendSnippet(snippets[i], options);
        }

        return text;
    }
}

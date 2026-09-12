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
    ///     An identifier as C# must spell it. A symbol's name is bare text, so a type, member or parameter
    ///     called <c>class</c> or <c>event</c> takes the <c>@</c> that makes it an identifier again.
    /// </summary>
    public static string Identifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    }

    /// <summary>
    ///     Appends <paramref name="name"/> the way <see cref="Identifier"/> spells it.
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
    ///     Appends a dotted name with every part spelled the way <see cref="Identifier"/> spells one.
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

        Append(Modifiers.File, "file");
        Append(Modifiers.Const, "const");
        Append(Modifiers.Static, "static");
        Append(Modifiers.Extern, "extern");
        Append(Modifiers.New, "new");
        Append(Modifiers.Virtual, "virtual");
        Append(Modifiers.Abstract, "abstract");
        Append(Modifiers.Sealed, "sealed");
        Append(Modifiers.Override, "override");
        Append(Modifiers.ReadOnly, "readonly");
        Append(Modifiers.Unsafe, "unsafe");
        Append(Modifiers.Required, "required");
        Append(Modifiers.Volatile, "volatile");
        Append(Modifiers.Async, "async");
        Append(Modifiers.Ref, "ref");
        Append(Modifiers.Partial, "partial");

        return text;

        void Append(Modifiers flag, string keyword)
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
    ///     The <c>ref</c> or <c>ref readonly</c> a return, property or field carries, with the space after it.
    ///     <c>RefKind.RefReadOnly</c> and <c>RefKind.In</c> are the same value. On a return it means
    ///     <c>ref readonly</c>.
    /// </summary>
    private static string RefText(RefKind refKind, RenderOptions options, string what)
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
    ///     The <c>ref</c>, <c>out</c>, <c>in</c> or <c>ref readonly</c> a parameter carries, with its space.
    /// </summary>
    private static string ParameterRefText(RefKind refKind, RenderOptions options, string what)
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

            if (parameter.IsThis)
            {
                text.Append("this ");
            }

            if (parameter.IsScoped)
            {
                text.Append("scoped ");
            }

            text.Append(ParameterRefText(parameter.RefKind, options, what));

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

            if (parameter.HasReferenceTypeConstraint)
            {
                bool annotated = parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    && options.NullableAnnotations
                    && options.Allows(CSharpVersion.CSharp8);
                Separate().Append(annotated ? "class?" : "class");
            }

            if (parameter.HasUnmanagedTypeConstraint)
            {
                Separate().Append("unmanaged");
            }
            else if (parameter.HasValueTypeConstraint)
            {
                Separate().Append("struct");
            }

            if (parameter.HasNotNullConstraint)
            {
                Separate().Append("notnull");
            }

            foreach (var constraintType in parameter.ConstraintTypes)
            {
                Separate().AppendType(constraintType, options);
            }

            if (parameter.HasConstructorConstraint)
            {
                Separate().Append("new()");
            }

            if (parameter.AllowsRefLikeType)
            {
                Separate().Append("allows ref struct");
            }

            StringBuilder Separate()
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

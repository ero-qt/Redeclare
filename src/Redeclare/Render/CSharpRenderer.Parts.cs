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
    ///     The <c>ref</c> or <c>ref readonly</c> a return carries, with the space after it.
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
                Require(options, CSharpVersion.CSharp7, "a ref return", what);
                return "ref ";
            }
            case RefKind.RefReadOnly:
            {
                Require(options, CSharpVersion.CSharp7_2, "a ref readonly return", what);
                return "ref readonly ";
            }
            default:
            {
                throw new RenderException($"{what} has ref kind {refKind}. A return may only be ref or ref readonly.");
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
                Require(options, CSharpVersion.CSharp7_2, "an in parameter", what);
                return "in ";
            }
            case RefKind.RefReadOnlyParameter:
            {
                Require(options, CSharpVersion.CSharp12, "a ref readonly parameter", what);
                return "ref readonly ";
            }
            default:
            {
                throw new RenderException($"{what} has a parameter with unknown ref kind {refKind}.");
            }
        }
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

    private static void Require(RenderOptions options, CSharpVersion since, string feature, string what)
    {
        if (!options.Allows(since))
        {
            throw RenderException.Needs(feature, what, since, options.Version);
        }
    }
}

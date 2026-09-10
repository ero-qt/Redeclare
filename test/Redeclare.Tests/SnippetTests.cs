using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace Redeclare.Tests;

[TestFixture]
public sealed class SnippetTests
{
    private static readonly NamedTypeReference _listOfInt = Types.List.Construct(Types.Int32);

    private static readonly RenderOptions _minimal = RenderOptions.Default with { Qualification = Qualification.Minimal };

    [Test]
    public void From_IndentedText_DedentsAndTrimsBlankEdges()
    {
        var snippet = Snippet.From("""

                if (x)
                {
                    return 1;
                }

                return 2;

            """);

        Assert.That(
            snippet.Lines,
            Is.EqualTo((EquatableArray<string>)
            [
                "if (x)",
                "{",
                "    return 1;",
                "}",
                "",
                "return 2;",
            ]));
    }

    [Test]
    public void IsSingleLine_OneLineOrMore_ReportsIt()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Snippet.From("a + b").IsSingleLine, Is.True);
            Assert.That(Snippet.From("a\n    + b").IsSingleLine, Is.False);
        }
    }

    [Test]
    public void IsEmpty_BlankTextOrEmpty_IsTrue()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Snippet.From("  \n ").IsEmpty, Is.True);
            Assert.That(Snippet.Empty.IsEmpty, Is.True);
            Assert.That(Snippet.From("a").IsEmpty, Is.False);
        }
    }

    [Test]
    public void Render_TypeHole_FillsItUnderTheOptionsInForce()
    {
        var snippet = Snippet.From($"var items = new {_listOfInt}();");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snippet.Render(RenderOptions.Default), Is.EqualTo("var items = new global::System.Collections.Generic.List<int>();"));
            Assert.That(snippet.Render(_minimal), Is.EqualTo("var items = new List<int>();"));
            Assert.That(snippet.Holes, Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void Render_GlobalHole_QualifiesWithGlobalWhateverTheOptionsSay()
    {
        Assert.That(Snippet.From($"{_listOfInt:g}").Render(_minimal), Is.EqualTo("global::System.Collections.Generic.List<int>"));
    }

    [Test]
    public void Render_FullHole_QualifiesWithTheNamespaceAlone()
    {
        Assert.That(Snippet.From($"{_listOfInt:f}").Render(_minimal), Is.EqualTo("System.Collections.Generic.List<int>"));
    }

    [Test]
    public void Render_MinimalHole_WritesTheNameAndTypeArgumentsOnly()
    {
        Assert.That(Snippet.From($"{_listOfInt:m}").Render(RenderOptions.Default), Is.EqualTo("List<int>"));
    }

    [Test]
    public void Render_NameOnlyHole_WritesTheBareName()
    {
        Assert.That(Snippet.From($"{_listOfInt:n}Extensions").Render(RenderOptions.Default), Is.EqualTo("ListExtensions"));
    }

    [Test]
    public void Render_NameOnlyHoleOnAnArray_Throws()
    {
        var array = new ArrayTypeReference(ElementType: Types.Int32);

        Assert.That(() => Snippet.From($"{array:n}").Render(RenderOptions.Default), Throws.TypeOf<RenderException>());
    }

    [Test]
    public void Render_UnknownHoleFormat_IsIgnoredLikeAnyOtherFormatSpecifier()
    {
        Assert.That(Snippet.From($"{_listOfInt:xyz}").Render(_minimal), Is.EqualTo("List<int>"));
    }

    [Test]
    public void From_StringWithLiteralFormat_WritesAnEscapedCSharpLiteral()
    {
        string quoted = "say \"hi\"\n";
        string? missing = null;

        Assert.That(Snippet.From($"{quoted:L}, {missing:L}").ToString(), Is.EqualTo("\"say \\\"hi\\\"\\n\", null"));
    }

    [Test]
    public void From_StringWithIdentifierFormat_EscapesKeywords()
    {
        string keyword = "class";
        string plain = "name";

        Assert.That(Snippet.From($"{keyword:I}, {plain:I}").ToString(), Is.EqualTo("@class, name"));
    }

    [Test]
    public void From_StringWithUnknownFormat_WritesTheTextAsIs()
    {
        string text = "abc";

        Assert.That(Snippet.From($"{text:xyz}").ToString(), Is.EqualTo("abc"));
    }

    [Test]
    public void From_PlainValues_ReadAsCSharpLiterals()
    {
        Snippet plain = "return 1;";
        var formatted = Snippet.From($"return {true} ? {1.5} : {2};");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plain.ToString(), Is.EqualTo("return 1;"));
            Assert.That(formatted.ToString(), Is.EqualTo("return true ? 1.5 : 2;"));
            Assert.That(formatted.Holes.IsEmpty, Is.True);
        }
    }

    [Test]
    public void Equals_SameTextAndHoles_IsTrueAndHashesAlike()
    {
        var a = Snippet.From($"new {_listOfInt}()");
        var b = Snippet.From($"new {Types.List.Construct(Types.Int32)}()");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }

    [Test]
    public void Equals_SameTextDifferentHole_IsFalse()
    {
        var a = Snippet.From($"new {_listOfInt}()");
        var otherType = Snippet.From($"new {Types.List.Construct(Types.Int64)}()");
        var otherFormat = Snippet.From($"new {_listOfInt:m}()");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Is.Not.EqualTo(otherType));
            Assert.That(a.Lines, Is.EqualTo(otherType.Lines), "the text is the same, only the hole differs");
            Assert.That(a, Is.Not.EqualTo(otherFormat), "a hole's format is part of its identity");
        }
    }

    [Test]
    public void From_SnippetInterpolatedIntoSnippet_SplicesAndRenumbersHoles()
    {
        var inner = Snippet.From($"({Types.String})x");
        var nullableObject = Types.Object with { NullableAnnotation = NullableAnnotation.Annotated };
        var outer = Snippet.From($"{_listOfInt} y = {inner};\nreturn {nullableObject};");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outer.Holes, Has.Length.EqualTo(3));
            Assert.That(outer.Render(_minimal), Is.EqualTo("List<int> y = (string)x;\nreturn object?;"));
        }
    }

    [Test]
    public void Join_SnippetsWithSeparator_JoinsOnOneLine()
    {
        var inner = Snippet.From($"({Types.String})x");
        var joined = Snippet.Join(", ", [inner, Snippet.From(Types.Int32), "3"]);

        Assert.That(joined.Render(_minimal), Is.EqualTo("(string)x, int, 3"));
    }

    [Test]
    public void ConcatAndAppend_Snippets_PutEachOnItsOwnLine()
    {
        var inner = Snippet.From($"({Types.String})x");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Snippet.Concat([inner, "next"]).Lines, Has.Length.EqualTo(2));
            Assert.That(inner.Append("b();").Lines, Has.Length.EqualTo(2));
        }
    }

    [Test]
    public void Concat_WithSelector_MakesOneLinePerItem()
    {
        var snippet = Snippet.Concat(["a", "b"], name => Snippet.From($"{Types.Int32} {name:I};"));

        Assert.That(snippet.Render(_minimal), Is.EqualTo("int a;\nint b;"));
    }

    [Test]
    public void Render_UnterminatedHoleMarkerInText_WritesItAsIs()
    {
        var snippet = Snippet.From("a" + Snippet.HoleStart + "b");

        Assert.That(snippet.Render(RenderOptions.Default), Is.EqualTo("a" + Snippet.HoleStart + "b"));
    }
}

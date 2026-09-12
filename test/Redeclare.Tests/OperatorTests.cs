using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Linq;

namespace Redeclare.Tests;

/// <summary>
///     Covers the name every user-defined operator and conversion reads back as.
/// </summary>
[TestFixture]
public sealed class OperatorTests
{
    private const string Source = """
        public class V
        {
            public static V operator +(V a, V b) => a;
            public static V operator -(V a, V b) => a;
            public static V operator *(V a, V b) => a;
            public static V operator /(V a, V b) => a;
            public static V operator %(V a, V b) => a;
            public static V operator &(V a, V b) => a;
            public static V operator |(V a, V b) => a;
            public static V operator ^(V a, V b) => a;
            public static V operator <<(V a, int b) => a;
            public static V operator >>(V a, int b) => a;
            public static V operator >>>(V a, int b) => a;
            public static bool operator ==(V a, V b) => true;
            public static bool operator !=(V a, V b) => false;
            public static bool operator <(V a, V b) => true;
            public static bool operator >(V a, V b) => true;
            public static bool operator <=(V a, V b) => true;
            public static bool operator >=(V a, V b) => true;
            public static V operator -(V a) => a;
            public static V operator +(V a) => a;
            public static bool operator !(V a) => true;
            public static V operator ~(V a) => a;
            public static V operator ++(V a) => a;
            public static V operator --(V a) => a;
            public static bool operator true(V a) => true;
            public static bool operator false(V a) => false;
            public static implicit operator int(V a) => 0;
            public static explicit operator long(V a) => 0;
            public static explicit operator short(V a) => 0;
            public static explicit operator checked short(V a) => 0;
            public static V operator checked +(V a, V b) => a;
            public static V operator checked -(V a) => a;
            public void operator +=(V other) { }
            public void operator -=(V other) { }
            public void operator >>>=(int count) { }
            public void operator checked -=(V other) { }
            public void operator ++() { }
        }
        """;

    private static CSharpCompilation Compilation => field ??= Compiling.AssertCompiles(Source);

    private static string[] Names => [.. Compilation.Type("V").ToDeclaration().Members.OfType<MethodDeclaration>().Select(m => m.Name)];

    [Test]
    public void ToDeclaration_EveryOverloadableOperator_ReadsAsItsToken()
    {
        Assert.That(
            Names.Order(StringComparer.Ordinal),
            Is.EqualTo(new[]
            {
                "explicit operator",
                "explicit operator",
                "explicit operator checked",
                "implicit operator",
                "operator !",
                "operator !=",
                "operator %",
                "operator &",
                "operator *",
                "operator +",
                "operator +",
                "operator ++",
                "operator ++",
                "operator +=",
                "operator -",
                "operator -",
                "operator --",
                "operator -=",
                "operator /",
                "operator <",
                "operator <<",
                "operator <=",
                "operator ==",
                "operator >",
                "operator >=",
                "operator >>",
                "operator >>>",
                "operator >>>=",
                "operator ^",
                "operator checked +",
                "operator checked -",
                "operator checked -=",
                "operator false",
                "operator true",
                "operator |",
                "operator ~",
            }));
    }

    [Test]
    public void Render_ExplicitInterfaceOperators_NameTheInterface()
    {
        var compilation = Compiling.AssertCompiles("""
            public interface IConv<T> where T : IConv<T>
            {
                static abstract implicit operator int(T value);
                static abstract explicit operator checked long(T value);
                static abstract explicit operator long(T value);
                static abstract T operator +(T a, T b);
            }

            public sealed class W : IConv<W>
            {
                static implicit IConv<W>.operator int(W value) => 0;
                static explicit IConv<W>.operator checked long(W value) => 0;
                static explicit IConv<W>.operator long(W value) => 0;
                static W IConv<W>.operator +(W a, W b) => a;
            }
            """);
        var w = compilation.Type("W").ToDeclaration();
        var withBodies = w with { Members = [.. w.Members.Select(m => m is MethodDeclaration method ? method with { Body = Snippet.Expression(method.Name == "operator +" ? "a" : "0") } : m)] };

        var text = new CompilationUnit(Members: [withBodies], Header: "// <auto-generated/>").Render();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("static implicit IConv<W>.operator int(W value)"));
            Assert.That(text, Does.Contain("static explicit IConv<W>.operator checked long(W value)"));
            Assert.That(text, Does.Contain("static W IConv<W>.operator +(W a, W b)"));
        }

        Compiling.AssertCompiles(
            text,
            LanguageVersion.Latest,
            "public interface IConv<T> where T : IConv<T> { static abstract implicit operator int(T value); static abstract explicit operator checked long(T value); static abstract explicit operator long(T value); static abstract T operator +(T a, T b); }");
    }

    [Test]
    public void ToDeclaration_InstanceOperators_ReadWithoutStatic()
    {
        var members = Compilation.Type("V").ToDeclaration().Members.OfType<MethodDeclaration>().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(members.Single(m => m.Name == "operator +=").Modifiers, Is.EqualTo(Modifiers.None));
            Assert.That(members.Single(m => m.Name == "operator checked +").Modifiers, Is.EqualTo(Modifiers.Static));
            Assert.That(members.Single(m => m.Name == "implicit operator").ReturnType, Is.EqualTo(Types.Int32));
        }
    }

    [Test]
    public void Render_ReadOperators_CompileAgain()
    {
        var type = Compilation.Type("V").ToDeclaration();
        var withBodies = type with
        {
            Members = [.. type.Members.Select(m => m switch
            {
                MethodDeclaration { ReturnType: NamedTypeReference { SpecialType: SpecialType.System_Boolean } } method
                    => method with { Body = Snippet.Expression("true") },
                MethodDeclaration { ReturnType: NamedTypeReference { SpecialType: SpecialType.System_Void } } method
                    => method with { Body = Snippet.Empty },
                MethodDeclaration { Name: "implicit operator" or "explicit operator" or "explicit operator checked" } method
                    => method with { Body = Snippet.Expression("0") },
                MethodDeclaration method => method with { Body = Snippet.Expression("a") },
                _ => m,
            })],
        };

        var text = new CompilationUnit(Members: [withBodies], Header: "// <auto-generated/>").Render();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("public static V operator >>>(V a, int b)"));
            Assert.That(text, Does.Contain("public static implicit operator int(V a)"));
            Assert.That(text, Does.Contain("public static explicit operator checked short(V a)"));
            Assert.That(text, Does.Contain("public static V operator checked +(V a, V b)"));
            Assert.That(text, Does.Contain("public void operator +=(V other)"));
            Assert.That(text, Does.Contain("public void operator checked -=(V other)"));
            Assert.That(text, Does.Contain("public void operator ++()"));
        }

        Compiling.AssertCompiles(text);
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Linq;

namespace Redeclare.Tests;

[TestFixture]
public sealed class ConstantTests
{
    private const string Source = """
        #nullable enable
        using System;
        using System.Collections.Generic;

        namespace Fixture
        {
            public enum Level : byte { Low, High = 5 }

            [AttributeUsage(AttributeTargets.All)]
            public sealed class MarkAttribute : Attribute
            {
                public MarkAttribute(string name, Level level = Level.Low, params int[] codes) { }
                public Type? Kind { get; set; }
                public string[]? Tags { get; set; }
            }

            [Mark("repo", Level.High, 1, 2, Kind = typeof(List<int>), Tags = new[] { "a", "b" })]
            [Serializable]
            public class Marked
            {
                public void G<T>(T unconstrained = default) { }
                public void H<T>(T reference = default) where T : class { }
                public void I<T>(T? nullableValue = default) where T : struct { }
                public void M(int a = 4, string? b = null, Level c = Level.High, Level d = (Level)9, ConsoleColor e = default, int? f = null, long g = 3, float h = 0.25f, decimal i = 1.5m, char j = 'x', bool k = true, int l = default, double m = double.NaN, float n = float.PositiveInfinity, double o = double.NegativeInfinity) { }
            }
        }
        """;

    private static CSharpCompilation Compilation => field ??= Compiling.AssertCompiles(Source);

    private static IMethodSymbol Method => Compilation.Type("Fixture.Marked").GetMembers("M").OfType<IMethodSymbol>().Single();

    [Test]
    public void ToSpecification_Attribute_ReadsArgumentsWithTypesAsHoles()
    {
        var mark = Compilation.Type("Fixture.Marked").GetAttributes().Single(a => a.AttributeClass!.Name == "MarkAttribute").ToSpecification()!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mark.Type.ToString(), Is.EqualTo("global::Fixture.MarkAttribute"));
            Assert.That(
                mark.Arguments.Select(a => a.ToString()),
                Is.EqualTo(new[]
                {
                    "\"repo\"",
                    "global::Fixture.Level.High",
                    "new int[] { 1, 2 }",
                    "Kind = typeof(global::System.Collections.Generic.List<int>)",
                    "Tags = new string[] { \"a\", \"b\" }",
                }));
            Assert.That(mark.Arguments[3].Render(RenderOptions.Default with { Qualification = Qualification.Minimal }), Is.EqualTo("Kind = typeof(List<int>)"));
        }
    }

    [Test]
    public void ToSpecification_AttributeWithoutArguments_HasNone()
    {
        var serializable = Compilation.Type("Fixture.Marked").GetAttributes().Single(a => a.AttributeClass!.Name == "SerializableAttribute").ToSpecification()!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(serializable.Type, Is.EqualTo(Types.SerializableAttribute));
            Assert.That(serializable.Arguments.IsEmpty, Is.True);
        }
    }

    [Test]
    public void ToSpecification_CompilerEmittedAttribute_IsNull()
    {
        // The compiler puts [Nullable] and [NullableContext] on members under #nullable enable. They are not source.
        var emitted = Method.Parameters[1].GetAttributes();

        Assert.That(emitted.Select(a => a.ToSpecification()), Has.All.Null);
    }

    [Test]
    public void Constant_ParameterDefaults_AreCSharpExpressions()
    {
        var defaults = Method.Parameters.Select(p => SymbolReader.FormatConstant(p.ExplicitDefaultValue, p.Type).ToString()).ToList();

        Assert.That(
            defaults,
            Is.EqualTo(new[]
            {
                "4",
                "null",
                "global::Fixture.Level.High",
                "(global::Fixture.Level)9",
                "global::System.ConsoleColor.Black",
                "null",
                "3",
                "0.25f",
                "1.5m",
                "'x'",
                "true",
                "0",
                "double.NaN",
                "float.PositiveInfinity",
                "double.NegativeInfinity",
            }));
    }

    [Test]
    public void Constant_DefaultOnATypeParameter_IsDefaultUnlessItIsKnownToBeAReference()
    {
        var type = Compilation.Type("Fixture.Marked");

        string Default(string method)
        {
            var parameter = type.GetMembers(method).OfType<IMethodSymbol>().Single().Parameters[0];

            return SymbolReader.FormatConstant(parameter.ExplicitDefaultValue, parameter.Type).ToString();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Default("G"), Is.EqualTo("default"), "an unconstrained T is neither a value nor a reference");
            Assert.That(Default("H"), Is.EqualTo("null"));
            Assert.That(Default("I"), Is.EqualTo("null"));
        }
    }

    [Test]
    public void Constant_NaN_SpellsTheTypeTheWayTheOptionsSay()
    {
        var parameter = Method.Parameters.Single(p => p.Name == "m");

        var snippet = SymbolReader.FormatConstant(parameter.ExplicitDefaultValue, parameter.Type);

        Assert.That(snippet.Render(RenderOptions.Default with { PredefinedTypeKeywords = false }), Is.EqualTo("global::System.Double.NaN"));
    }
}

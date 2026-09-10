using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Redeclare.Tests;

[TestFixture]
public sealed class TypeReferenceOracleTests
{
    private const string Source = """
        #nullable enable
        using System;
        using System.Collections.Generic;

        namespace Oracle
        {
            public struct Point { public int X; }
            public class @class { }
            public class Outer<T> { public class Inner<U> { } }

            public unsafe ref struct Shapes<T> where T : struct
            {
                public int A;
                public int? B;
                public string? C;
                public int[] D;
                public int[,] E;
                public int[][] F;
                public string?[] G;
                public List<string?> H;
                public Dictionary<int, List<string>> I;
                public Outer<int>.Inner<string> J;
                public (int a, string b) K;
                public (int, (string x, double y)) L;
                public dynamic M;
                public nint N;
                public nuint O;
                public int* P;
                public void* Q;
                public Point* R;
                public T S;
                public T? T2;
                public List<int>[] U;
                public Dictionary<int, string>.Enumerator V;
                public object W;
                public Span<int> X;
                public delegate*<int, void> Y;
                public int[,][] Z;
                public int[]?[] AC;
                public int[][,] AD;
                public string?[][] AE;
                public int[,][]? AF;
                public int*[] AG;
                public Point*[]? AH;
                public List<dynamic> AI;
                public List<int?> AJ;
                public List<T?> AK;
                public Nullable<int> AL;
                public (int, string)? AM;
                public ((int a, string b) inner, double c) AN;
                public Outer<int?>.Inner<string?>? AO;
                public global::Oracle.Point AP;
                public @class AQ;
                public List<List<List<int>>> AR;
                public IEnumerable<KeyValuePair<string, List<int[]>>> AS;
                public delegate*<int, string?, void> AT;
                public delegate* unmanaged[Cdecl]<int, void> AX;
                public delegate* unmanaged[Cdecl, SuppressGCTransition]<int, int> AY;
                public delegate* unmanaged<void> AZ;
                public delegate*<ref int, ref readonly int> BA;
                public delegate*<in int, out int, void> BB;
                public delegate* managed<int, void> BC;
                public delegate*<delegate*<int, void>, void> BD;
                public Span<Point> AU;
                public object? AV;
                public void*[] AW;
                public List<Outer<string?>.Inner<int[]>> AA;
                public IEnumerable<(int, string?)> AB;
            }
        }
        """;

    [Test]
    public void ToTypeReference_EveryShape_RendersAsRoslynDisplaysIt()
    {
        var compilation = Compiling.Compile(Source);
        var shapes = compilation.Type("Oracle.Shapes`1");
        var format = SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        List<string> mismatches = [];
        foreach (var field in shapes.GetMembers().OfType<IFieldSymbol>())
        {
            string expected = field.Type.ToDisplayString(format);
            string actual = field.Type.ToTypeReference().ToString();
            if (expected != actual)
            {
                mismatches.Add($"{field.Name}: roslyn '{expected}' vs redeclare '{actual}'");
            }
        }

        Assert.That(mismatches, Is.Empty, () => string.Join("\n", mismatches));
    }

    [Test]
    public void ToTypeReference_EveryShape_BindsBackToTheSameSymbol()
    {
        // The stronger oracle: not "does this match Roslyn's display" but "does the compiler read what we wrote as the type
        // we meant". Every field type of a closed Shapes<int> is rendered, written back out as a parameter, compiled, and
        // compared as a symbol.
        var compilation = Compiling.Compile(Source);
        var shapes = compilation.Type("Oracle.Shapes`1").Construct(compilation.GetSpecialType(SpecialType.System_Int32));
        var fields = shapes.GetMembers().OfType<IFieldSymbol>().ToList();

        StringBuilder probe = new();
        probe.AppendLine("#nullable enable").AppendLine("unsafe class Probe").AppendLine("{");
        for (int i = 0; i < fields.Count; i++)
        {
            probe.Append("    void M").Append(i).Append('(').Append(fields[i].Type.ToTypeReference().ToString()).AppendLine(" p) { }");
        }

        probe.AppendLine("}");

        var round = Compiling.AssertCompiles(Source, LanguageVersion.Latest, probe.ToString());
        var methods = round.Type("Probe").GetMembers().OfType<IMethodSymbol>().Where(m => m.Name.StartsWith('M')).ToList();
        var closed = round.Type("Oracle.Shapes`1").Construct(round.GetSpecialType(SpecialType.System_Int32));
        var expected = closed.GetMembers().OfType<IFieldSymbol>().ToList();

        List<string> mismatches = [];
        for (int i = 0; i < expected.Count; i++)
        {
            var wrote = methods.Single(m => m.Name == "M" + i.ToString(CultureInfo.InvariantCulture)).Parameters[0].Type;
            if (!SymbolEqualityComparer.IncludeNullability.Equals(wrote, expected[i].Type))
            {
                mismatches.Add($"{expected[i].Name}: wrote a {wrote} where the symbol was a {expected[i].Type}");
            }
        }

        Assert.That(mismatches, Is.Empty, () => string.Join("\n", mismatches));
    }

    [Test]
    public void ToTypeReference_StructConstrainedTypeParameter_IsValueType()
    {
        var compilation = Compiling.Compile(Source);
        var shapes = compilation.Type("Oracle.Shapes`1");

        var t = shapes.TypeParameters[0].ToTypeReference();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(t, Is.EqualTo(new TypeParameterReference(Name: "T", HasValueTypeConstraint: true)));
            Assert.That(t.IsValueType, Is.True);
        }
    }

    [Test]
    public void ToTypeReference_NullableOfT_IsTheArgumentAnnotated()
    {
        var compilation = Compiling.Compile(Source);
        var shapes = compilation.Type("Oracle.Shapes`1");
        var al = shapes.GetMembers("AL").OfType<IFieldSymbol>().Single();

        Assert.That(al.Type.ToTypeReference(), Is.EqualTo(Types.Nullable(Types.Int32)));
    }

    [Test]
    public void ToTypeReference_NestedGeneric_KeepsContainingTypeArguments()
    {
        var compilation = Compiling.Compile(Source);
        var shapes = compilation.Type("Oracle.Shapes`1");
        var j = (NamedTypeReference)shapes.GetMembers("J").OfType<IFieldSymbol>().Single().Type.ToTypeReference();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(j.ContainingNamespace, Is.Null);
            Assert.That(j.ContainingType!.ContainingNamespace, Is.EqualTo("Oracle"));
            Assert.That(j.ContainingType.TypeArguments, Is.EqualTo((EquatableArray<TypeReference>)[Types.Int32]));
            Assert.That(j.TypeArguments, Is.EqualTo((EquatableArray<TypeReference>)[Types.String]));
        }
    }

    [Test]
    public void ToTypeReference_UnmanagedFunctionPointer_DropsTheCallConvPrefix()
    {
        var compilation = Compiling.Compile(Source);
        var shapes = compilation.Type("Oracle.Shapes`1");
        var ay = (FunctionPointerTypeReference)shapes.GetMembers("AY").OfType<IFieldSymbol>().Single().Type.ToTypeReference();

        Assert.That(ay.UnmanagedCallingConventionTypes, Is.EqualTo((EquatableArray<string>)["Cdecl", "SuppressGCTransition"]));
    }
}

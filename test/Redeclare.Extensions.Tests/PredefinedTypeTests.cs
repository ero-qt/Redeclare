using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Redeclare.Extensions.Tests;

[TestFixture]
public sealed class PredefinedTypeTests
{
    private static readonly Dictionary<string, NamedTypeReference> _byKeyword = new()
    {
        ["object"] = TypeReference.Object,
        ["string"] = TypeReference.String,
        ["bool"] = TypeReference.Boolean,
        ["char"] = TypeReference.Char,
        ["sbyte"] = TypeReference.SByte,
        ["byte"] = TypeReference.Byte,
        ["short"] = TypeReference.Int16,
        ["ushort"] = TypeReference.UInt16,
        ["int"] = TypeReference.Int32,
        ["uint"] = TypeReference.UInt32,
        ["long"] = TypeReference.Int64,
        ["ulong"] = TypeReference.UInt64,
        ["float"] = TypeReference.Single,
        ["double"] = TypeReference.Double,
        ["decimal"] = TypeReference.Decimal,
        ["void"] = TypeReference.Void,
        ["nint"] = TypeReference.NativeInt,
        ["nuint"] = TypeReference.NativeUInt,
    };

    [Test]
    public void Predefined_EveryOne_RendersAsItsKeyword()
    {
        var rendered = _byKeyword.ToDictionary(pair => pair.Key, pair => pair.Value.ToString());

        Assert.That(rendered, Is.EqualTo(_byKeyword.Keys.ToDictionary(keyword => keyword, keyword => keyword)));
    }

    [Test]
    public void Predefined_WithoutKeywords_RenderAsTheirFrameworkName()
    {
        var options = RenderOptions.Default with { PredefinedTypeKeywords = false };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CSharpRenderer.RenderType(TypeReference.Int32, options), Is.EqualTo("global::System.Int32"));
            Assert.That(CSharpRenderer.RenderType(TypeReference.Void, options), Is.EqualTo("void"), "System.Void is not a type C# can write");
            Assert.That(CSharpRenderer.RenderType(TypeReference.NativeInt, options), Is.EqualTo("global::System.IntPtr"));
        }
    }

    [Test]
    public void Predefined_MatchWhatTheReaderProduces()
    {
        // The reader is the reference: a predefined type named by hand must equal the same type read from a symbol.
        var compilation = Compiling.Compile("class C { int i; string s; void* p; nint n; }");
        var fields = compilation.Type("C").GetMembers().OfType<IFieldSymbol>().ToDictionary(f => f.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["i"].Type.ToTypeReference(), Is.EqualTo(TypeReference.Int32));
            Assert.That(fields["s"].Type.ToTypeReference(), Is.EqualTo(TypeReference.String));
            Assert.That(fields["n"].Type.ToTypeReference(), Is.EqualTo(TypeReference.NativeInt));
        }
    }

    [Test]
    public void Predefined_ValueTypes_ReportThemselvesAsSuch()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeReference.Int32.IsValueType, Is.True);
            Assert.That(TypeReference.String.IsValueType, Is.False);
            Assert.That(TypeReference.Object.IsValueType, Is.False);
        }
    }
}

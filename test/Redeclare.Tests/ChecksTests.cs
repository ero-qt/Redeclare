using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Linq;

namespace Redeclare.Tests;

[TestFixture]
public sealed class ChecksTests
{
    private const string Fixture = """
        using System;
        using System.Collections.Generic;

        namespace Fixture
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
            public sealed class OnTypesAttribute : Attribute { }

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class OnMethodsAttribute : Attribute { }

            public sealed class AnywhereAttribute : Attribute { }

            [OnTypes]
            public partial class Whole { public partial void Half(); }
            public partial class Whole { public partial void Half() { } public void Full() { } }

            public class NotPartial { }

            public class Outer
            {
                public partial class Inner { }
            }

            public partial class OuterPartial
            {
                public partial class Inner { }
            }

            public class Derived : List<int>, IDisposable
            {
                public void Dispose() { }
                public struct Nested<T> { }
            }
        }
        """;

    private static CSharpCompilation Compilation => field ??= Compiling.AssertCompiles(Fixture);

    [Test]
    public void IsPartial_Type_NeedsEveryDeclarationPartial()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Compilation.Type("Fixture.Whole").IsPartial(), Is.True);
            Assert.That(Compilation.Type("Fixture.NotPartial").IsPartial(), Is.False);
            Assert.That(Compilation.GetSpecialType(SpecialType.System_String).IsPartial(), Is.False, "metadata types have no declarations");
        }
    }

    [Test]
    public void IsPartialThroughout_NestedType_NeedsContainingTypesPartial()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Compilation.Type("Fixture.Outer+Inner").IsPartial(), Is.True);
            Assert.That(Compilation.Type("Fixture.Outer+Inner").IsPartialThroughout(), Is.False);
            Assert.That(Compilation.Type("Fixture.OuterPartial+Inner").IsPartialThroughout(), Is.True);
        }
    }

    [Test]
    public void IsPartial_Method_IsTrueForEitherHalf()
    {
        var whole = Compilation.Type("Fixture.Whole");
        var half = whole.GetMembers("Half").OfType<IMethodSymbol>().First();
        var full = whole.GetMembers("Full").OfType<IMethodSymbol>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(half.IsPartial(), Is.True);
            Assert.That(full.IsPartial(), Is.False);
        }
    }

    [Test]
    public void Is_ConstructedType_IsItsDefinition()
    {
        var derived = Compilation.Type("Fixture.Derived");
        var nested = Compilation.Type("Fixture.Derived+Nested`1");
        var list = Compilation.Type("System.Collections.Generic.List`1");
        var int32 = Compilation.GetSpecialType(SpecialType.System_Int32);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(derived.Is(derived), Is.True);
            Assert.That(derived.Is(list), Is.False);
            Assert.That(derived.BaseType!.Is(list), Is.True);
            Assert.That(nested.Construct(int32).Is(nested), Is.True);
        }
    }

    [Test]
    public void InheritsFromAndImplements_Derived_WalkTheHierarchy()
    {
        var derived = Compilation.Type("Fixture.Derived");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(derived.InheritsFrom(Compilation.Type("System.Collections.Generic.List`1")), Is.True);
            Assert.That(derived.InheritsFrom(Compilation.GetSpecialType(SpecialType.System_Object)), Is.True);
            Assert.That(derived.Implements(Compilation.Type("System.IDisposable")), Is.True);
            Assert.That(derived.Implements(Compilation.Type("System.Collections.Generic.IList`1")), Is.True, "through the base class");
            Assert.That(derived.Implements(Compilation.Type("System.IComparable")), Is.False);
        }
    }

    [Test]
    public void FullMetadataName_NestedGeneric_RoundTripsThroughTheCompilation()
    {
        var nested = Compilation.Type("Fixture.Derived+Nested`1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nested.FullMetadataName(), Is.EqualTo("Fixture.Derived+Nested`1"));
            Assert.That(Compilation.GetTypeByMetadataName(nested.FullMetadataName()), Is.SameAs(nested));
        }
    }

    [Test]
    public void ValidTargets_AttributeUsage_DecidesWhereAnAttributeMayGo()
    {
        var whole = Compilation.Type("Fixture.Whole");
        var method = whole.GetMembers("Full").Single();
        var onTypesClass = Compilation.Type("Fixture.OnTypesAttribute");
        var onTypes = whole.GetAttribute(onTypesClass)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(onTypesClass.ValidTargets(Compilation), Is.EqualTo(AttributeTargets.Class | AttributeTargets.Struct));
            Assert.That(onTypes.IsValidOn(whole, Compilation), Is.True);
            Assert.That(onTypes.IsValidOn(method, Compilation), Is.False);
            Assert.That(Compilation.Type("Fixture.OnMethodsAttribute").ValidTargets(Compilation), Is.EqualTo(AttributeTargets.Method));
            Assert.That(Compilation.Type("Fixture.AnywhereAttribute").ValidTargets(Compilation), Is.EqualTo(AttributeTargets.All), "no usage means all");
        }
    }

    [Test]
    public void AllowsMultiple_AttributeUsage_ReadsTheNamedArgument()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Compilation.Type("Fixture.OnTypesAttribute").AllowsMultiple(Compilation), Is.True);
            Assert.That(Compilation.Type("Fixture.OnMethodsAttribute").AllowsMultiple(Compilation), Is.False);
        }
    }

    [Test]
    public void AttributeTarget_Symbol_IsItsAttributeTargetsFlag()
    {
        var whole = Compilation.Type("Fixture.Whole");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whole.AttributeTarget(), Is.EqualTo(AttributeTargets.Class));
            Assert.That(whole.GetMembers("Full").Single().AttributeTarget(), Is.EqualTo(AttributeTargets.Method));
        }
    }
}

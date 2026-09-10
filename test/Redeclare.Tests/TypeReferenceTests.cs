using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System.Reflection.Metadata;

namespace Redeclare.Tests;

[TestFixture]
public sealed class TypeReferenceTests
{
    private static readonly RenderOptions _minimal = RenderOptions.Default with { Qualification = Qualification.Minimal };

    [Test]
    public void RenderType_ThreeQualifications_SpellsOneTypeThreeWays()
    {
        var type = Types.List.Construct(Types.StringBuilder with { NullableAnnotation = NullableAnnotation.Annotated })
            with { NullableAnnotation = NullableAnnotation.Annotated };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(type.ToString(), Is.EqualTo("global::System.Collections.Generic.List<global::System.Text.StringBuilder?>?"));
            Assert.That(
                CSharpRenderer.RenderType(type, RenderOptions.Default with { Qualification = Qualification.Full }),
                Is.EqualTo("System.Collections.Generic.List<System.Text.StringBuilder?>?"));
            Assert.That(CSharpRenderer.RenderType(type, _minimal), Is.EqualTo("List<StringBuilder?>?"));
        }
    }

    [Test]
    public void RenderType_PredefinedTypes_WritesKeywordsUnlessToldNotTo()
    {
        var nint = Types.Int32 with { Name = "IntPtr", SpecialType = SpecialType.System_IntPtr, IsNativeIntegerType = true };
        var names = RenderOptions.Default with { PredefinedTypeKeywords = false };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Types.Int32.ToString(), Is.EqualTo("int"));
            Assert.That(Types.String.ToString(), Is.EqualTo("string"));
            Assert.That(nint.ToString(), Is.EqualTo("nint"));
            Assert.That(CSharpRenderer.RenderType(Types.Int32, names), Is.EqualTo("global::System.Int32"));
            Assert.That(CSharpRenderer.RenderType(Types.Void, names), Is.EqualTo("void"), "void has no other spelling");
        }
    }

    [Test]
    public void RenderType_Arrays_WritesOuterRankFirstWithAnnotationsShifted()
    {
        var nullableInt = Types.Int32 with { NullableAnnotation = NullableAnnotation.Annotated };
        var jagged = new ArrayTypeReference(ElementType: new ArrayTypeReference(ElementType: Types.Int32), Rank: 2);
        var nullableMatrix = new ArrayTypeReference(ElementType: Types.Int32, Rank: 2, NullableAnnotation: NullableAnnotation.Annotated);
        var arrayOfNullableArrays = new ArrayTypeReference(
            ElementType: new ArrayTypeReference(ElementType: Types.Int32, NullableAnnotation: NullableAnnotation.Annotated));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nullableMatrix.ToString(), Is.EqualTo("int[,]?"));
            Assert.That(new ArrayTypeReference(ElementType: nullableInt).ToString(), Is.EqualTo("int?[]"));
            Assert.That(jagged.ToString(), Is.EqualTo("int[,][]"), "an array of int[] with rank two is int[,][]");
            Assert.That(arrayOfNullableArrays.ToString(), Is.EqualTo("int[]?[]"));
        }
    }

    [Test]
    public void RenderType_TuplesPointersParametersAndDynamic_SpellsThemAsCSharpDoes()
    {
        var tuple = new TupleTypeReference(Elements: [new TupleElement(Type: Types.Int32, Name: "a"), new TupleElement(Type: Types.String)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tuple.ToString(), Is.EqualTo("(int a, string)"));
            Assert.That(new PointerTypeReference(PointedAtType: Types.Int32).ToString(), Is.EqualTo("int*"));
            Assert.That(new TypeParameterReference(Name: "T", NullableAnnotation: NullableAnnotation.Annotated).ToString(), Is.EqualTo("T?"));
            Assert.That(new DynamicTypeReference().ToString(), Is.EqualTo("dynamic"));
            Assert.That(new ErrorTypeReference(Text: "Missing<int>").ToString(), Is.EqualTo("Missing<int>"), "an error type renders as written");
        }
    }

    [Test]
    public void RenderType_NestedAndUnboundGenerics_QualifiesThroughTheOutermostType()
    {
        var container = new NamedTypeReference(Name: "Container", ContainingNamespace: "Outer", Arity: 1).Construct(Types.Int32);
        var inner = new NamedTypeReference(Name: "Inner", ContainingType: container);
        var dictionary = new NamedTypeReference(Name: "Dictionary", ContainingNamespace: "System.Collections.Generic", Arity: 2);
        var enumerator = new NamedTypeReference(Name: "Enumerator", ContainingType: dictionary, TypeKind: TypeKind.Struct);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner.ToString(), Is.EqualTo("global::Outer.Container<int>.Inner"));
            Assert.That(
                enumerator.ToString(),
                Is.EqualTo("global::System.Collections.Generic.Dictionary<,>.Enumerator"),
                "an unbound generic renders as typeof would want it");
            Assert.That(dictionary.MetadataName, Is.EqualTo("Dictionary`2"));
            Assert.That(dictionary.IsUnboundGenericType, Is.True);
        }
    }

    [Test]
    public void RenderType_NamesThatAreKeywords_EscapesThem()
    {
        var type = new NamedTypeReference(Name: "class", ContainingNamespace: "event.struct");

        Assert.That(type.ToString(), Is.EqualTo("global::@event.@struct.@class"));
    }

    [Test]
    public void Construct_WrongNumberOfArguments_Throws()
    {
        Assert.That(() => Types.List.Construct(Types.Int32, Types.Int32), Throws.ArgumentException);
    }

    [Test]
    public void RenderType_AnnotationsOffOrBelowCSharp8_DropsTheQuestionMarkOnReferenceTypesOnly()
    {
        var reference = Types.String with { NullableAnnotation = NullableAnnotation.Annotated };
        var value = Types.Int32 with { NullableAnnotation = NullableAnnotation.Annotated };
        var old = RenderOptions.Default with { Version = CSharpVersion.CSharp7_3 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CSharpRenderer.RenderType(reference, RenderOptions.Default with { NullableAnnotations = false }), Is.EqualTo("string"));
            Assert.That(CSharpRenderer.RenderType(reference, old), Is.EqualTo("string"));
            Assert.That(CSharpRenderer.RenderType(value, old), Is.EqualTo("int?"));
        }
    }

    [Test]
    public void RenderType_FunctionPointer_WritesTheCallingConventionAndNeedsCSharp9()
    {
        var pointer = new FunctionPointerTypeReference(
            ReturnType: Types.Void,
            Parameters: [new FunctionPointerParameter(Type: Types.Int32), new FunctionPointerParameter(Type: Types.Int32, RefKind: RefKind.Ref)],
            CallingConvention: SignatureCallingConvention.CDecl);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pointer.ToString(), Is.EqualTo("delegate* unmanaged[Cdecl]<int, ref int, void>"));
            Assert.That(
                () => CSharpRenderer.RenderType(pointer, RenderOptions.Default with { Version = CSharpVersion.CSharp7_2 }),
                Throws.TypeOf<RenderException>().With.Message.Contains("C# 9").And.Message.Contains("C# 7.2"));
        }
    }
}

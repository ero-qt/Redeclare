using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace Redeclare.Extensions.Tests;

[TestFixture]
public sealed class ToArgumentTests
{
    [TestCase(RefKind.None, "value")]
    [TestCase(RefKind.Ref, "ref value")]
    [TestCase(RefKind.Out, "out value")]
    [TestCase(RefKind.In, "in value")]
    [TestCase(RefKind.RefReadOnlyParameter, "in value")]
    public void ToArgument_EachRefKind_WritesTheModifierAndName(RefKind refKind, string expected)
    {
        var parameter = new ParameterDeclaration(Type: TypeReference.Int32, Name: "value", RefKind: refKind);

        Assert.That(parameter.ToArgument().Render(RenderOptions.Default), Is.EqualTo(expected));
    }

    [Test]
    public void ToArgument_KeywordName_WritesAt()
    {
        var parameter = new ParameterDeclaration(Type: TypeReference.Int32, Name: "class");

        Assert.That(parameter.ToArgument().Render(RenderOptions.Default), Is.EqualTo("@class"));
    }

    [Test]
    public void ToArguments_ParamsAndThis_WritesBareNamesJoined()
    {
        EquatableArray<ParameterDeclaration> parameters =
        [
            new(Type: TypeReference.String, Name: "self", IsThis: true),
            new(Type: TypeReference.Int32, Name: "count", RefKind: RefKind.Out),
            new(Type: TypeReference.Int32, Name: "rest", IsParams: true),
        ];

        Assert.That(parameters.ToArguments().Render(RenderOptions.Default), Is.EqualTo("self, out count, rest"));
    }

    [Test]
    public void ToArguments_NoParameters_WritesNothing()
    {
        Assert.That(default(EquatableArray<ParameterDeclaration>).ToArguments().IsEmpty, Is.True);
    }
}

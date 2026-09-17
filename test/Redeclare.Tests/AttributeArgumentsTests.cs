using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Linq;

namespace Redeclare.Tests;

[TestFixture]
public sealed class AttributeArgumentsTests
{
    /// <summary>
    ///     A generator-side mirror of the fixture's <c>Level</c> enum, as generators usually keep one.
    /// </summary>
    private enum LevelMirror
    {
        Low = 0,
        High = 5,
    }

    private static CSharpCompilation Compilation => field ??= Compiling.AssertCompiles(SymbolReaderTests.FixtureSource);

    private static INamedTypeSymbol MarkAttribute => Compilation.Type("Fixture.MarkAttribute");

    private static INamedTypeSymbol TagAttribute => Compilation.Type("Fixture.TagAttribute`1");

    private static INamedTypeSymbol Repository => Compilation.Type("Fixture.Repository`1");

    private static INamedTypeSymbol Extensions => Compilation.Type("Fixture.Extensions");

    private static AttributeArguments Mark => Repository.GetAttribute(MarkAttribute)!.GetArguments();

    [Test]
    public void GetAttribute_ByClassSymbol_FindsOnlyThatClass()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Repository.GetAttribute(MarkAttribute), Is.Not.Null);
            Assert.That(Repository.GetAttribute(TagAttribute), Is.Null);
            Assert.That(Repository.GetAttribute(Compilation.Type("System.SerializableAttribute")), Is.Null);
            Assert.That(Extensions.GetAttributes(MarkAttribute).Count(), Is.EqualTo(2));
            Assert.That(Repository.GetAttribute(MarkAttribute)!.IsOfClass(MarkAttribute), Is.True);
        }
    }

    [Test]
    public void GetAttribute_ConstructedGeneric_MatchesItsDefinition()
    {
        Assert.That(Extensions.GetAttribute(TagAttribute), Is.Not.Null);
    }

    [Test]
    public void ConstructorArgument_ByParameterName_ReadsAndConverts()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Mark.GetConstructorArgument<string>("name"), Is.EqualTo("repo"));
            Assert.That(Mark.GetConstructorArgument<LevelMirror>("level"), Is.EqualTo(LevelMirror.High));
            Assert.That(Mark.GetConstructorArgument<int>("level"), Is.EqualTo(5), "an enum reads as its underlying value too");
            Assert.That(Mark.GetConstructorArray<int>("codes"), Is.EqualTo((EquatableArray<int>)[1, 2]));
            Assert.That(Mark.GetConstructorArgument("missing", 9), Is.EqualTo(9));
            Assert.That(Mark.TryGetConstructorArgument<string>("Name", out _), Is.False, "names match exactly, as C# does");
        }
    }

    [Test]
    public void ConstructorArgument_ByPosition_ReadsAndConverts()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Mark.GetConstructorArgument<string>(0), Is.EqualTo("repo"));
            Assert.That(Mark.GetConstructorArgument<LevelMirror>(1), Is.EqualTo(LevelMirror.High));
            Assert.That(Mark.GetConstructorArgument(7, 9), Is.EqualTo(9));
            Assert.That(Mark.TryGetConstructorArgument<string>(0, out var name) && name == "repo", Is.True);
            Assert.That(Mark.TryGetConstructorArgument<string>(7, out _), Is.False);
            Assert.That(Mark.GetConstructorArray<int>(2), Is.EqualTo((EquatableArray<int>)[1, 2]));
            Assert.That(Mark.GetConstructorExpression(1)!.ToString(), Is.EqualTo("global::Fixture.Level.High"));
        }
    }

    [Test]
    public void NamedArgument_ByPropertyName_ReadsAndConverts()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Mark.GetNamedArgument<TypeReference>("Kind"), Is.EqualTo(Types.List.Construct(Types.Int32)));
            Assert.That(Mark.GetNamedArgument<ITypeSymbol>("Kind")!.Name, Is.EqualTo("List"));
            Assert.That(Mark.GetNamedArgument<ITypeSymbol>("kind"), Is.Null, "names match exactly, as C# does");
            Assert.That(Mark.TryGetNamedArgument<TypeReference>("Kind", out _), Is.True);
            Assert.That(Mark.TryGetNamedArgument<string>("name", out _), Is.False, "a constructor parameter is not a named argument");
            Assert.That(Mark.GetNamedArgument("Missing", 42), Is.EqualTo(42));
        }
    }

    [Test]
    public void Expression_EitherList_IsCSharp()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Mark.GetConstructorExpression("level")!.ToString(), Is.EqualTo("global::Fixture.Level.High"));
            Assert.That(Mark.GetNamedExpression("Kind")!.ToString(), Is.EqualTo("typeof(global::System.Collections.Generic.List<int>)"));
            Assert.That(Mark.TypeArguments.IsEmpty, Is.True);
        }
    }

    [Test]
    public void ConstructorArgument_Omitted_FallsBackToTheParameterDefault()
    {
        var marks = Extensions.GetAttributes(MarkAttribute).Select(a => a.GetArguments()).ToList();
        var positional = marks.Single(m => m.GetConstructorArgument<string>("name") == "ext");
        var named = marks.Single(m => m.GetConstructorArgument<string>("name") == "named");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(positional.GetConstructorArgument<LevelMirror>("level"), Is.EqualTo(LevelMirror.Low));
            Assert.That(positional.TryGetConstructorArgument<LevelMirror>("level", out _), Is.True, "a default counts as present");
            Assert.That(positional.GetConstructorArgument<LevelMirror>(1), Is.EqualTo(LevelMirror.Low), "by position falls back the same way");
            Assert.That(positional.GetConstructorExpression(1)!.ToString(), Is.EqualTo("global::Fixture.Level.Low"));
            Assert.That(positional.GetConstructorArray<int>("codes"), Is.EqualTo((EquatableArray<int>)[7]));
            Assert.That(positional.TryGetNamedArgument<TypeReference>("Kind", out _), Is.False, "a property never set has no default to fall back to");
            Assert.That(positional.GetNamedArgument<TypeReference>("Kind"), Is.Null);
            Assert.That(named.GetConstructorArgument<LevelMirror>("level"), Is.EqualTo(LevelMirror.High), "named constructor arguments resolve by parameter");
            Assert.That(named.GetConstructorArray<int>("codes").IsEmpty, Is.True);
        }
    }

    [Test]
    public void GetArguments_ParameterAndPropertyWithOneSpelling_AreTwoArguments()
    {
        var clash = Extensions.GetAttribute(Compilation.Type("Fixture.ClashAttribute"))!.GetArguments();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clash.GetConstructorArgument<string>("Value"), Is.EqualTo("from constructor"));
            Assert.That(clash.GetNamedArgument<string>("Value"), Is.EqualTo("from property"));
            Assert.That(clash.GetConstructorArgument<string>(0), Is.EqualTo("from constructor"));
            Assert.That(clash.GetNamedExpression("Value")!.ToString(), Is.EqualTo("\"from property\""));
        }
    }

    [Test]
    public void TryGet_ArgumentOfAnotherKind_IsAMiss()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Mark.TryGetConstructorArgument<LevelMirror>("name", out _), Is.False, "a string is not an enum");
            Assert.That(Mark.GetConstructorArgument("name", LevelMirror.Low), Is.EqualTo(LevelMirror.Low));
            Assert.That(Mark.TryGetConstructorArgument<string>("name", out string? name), Is.True);
            Assert.That(name, Is.EqualTo("repo"));
            Assert.That(Mark.TryGetNamedArgument<TypeReference>("Kind", out var kind), Is.True);
            Assert.That(kind, Is.EqualTo(Types.List.Construct(Types.Int32)));
            Assert.That(Mark.TryGetNamedArgument<string>("Missing", out _), Is.False);
            Assert.That(Mark.Data.AttributeClass!.Name, Is.EqualTo("MarkAttribute"));
        }
    }

    [Test]
    public void Array_AbsentOrNotAnArray_IsEmpty()
    {
        var onExtensions = Extensions.GetAttributes(MarkAttribute).First().GetArguments();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(onExtensions.GetConstructorArray<int>("codes"), Is.EqualTo((EquatableArray<int>)[7]));
            Assert.That(onExtensions.GetNamedArray<int>("Missing"), Is.Empty);
            Assert.That(onExtensions.GetConstructorArray<int>("name"), Is.Empty);
        }
    }

    [Test]
    public void TypeArguments_GenericAttribute_AreExposed()
    {
        var tag = Extensions.GetAttribute(TagAttribute)!.GetArguments();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tag.TypeArguments, Is.EqualTo((EquatableArray<TypeReference>)[Types.Int32]));
            Assert.That(tag.TypeArgumentSymbols.Single().SpecialType, Is.EqualTo(SpecialType.System_Int32));
            Assert.That(tag.GetConstructorArgument<string>("label"), Is.EqualTo("tagged"));
        }
    }
}

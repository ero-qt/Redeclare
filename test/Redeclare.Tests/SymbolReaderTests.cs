using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;

namespace Redeclare.Tests;

[TestFixture]
public sealed class SymbolReaderTests
{
    internal const string FixtureSource = """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using System.Runtime.CompilerServices;

        namespace Fixture
        {
            public enum Level : byte { Low, High = 5 }

            [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
            public sealed class MarkAttribute : Attribute
            {
                public MarkAttribute(string name, Level level = Level.Low, params int[] codes) { }
                public Type? Kind { get; set; }
            }

            public interface IRepository<in TKey, out TValue> where TKey : notnull
            {
                TValue Get(TKey key);
                int Count { get; }
                static abstract IRepository<TKey, TValue> Empty { get; }
            }

            /// <summary>
            ///     A repository.
            /// </summary>
            /// <typeparam name="T">The item.</typeparam>
            [Mark("repo", Level.High, 1, 2, Kind = typeof(List<int>))]
            public abstract partial class Repository<T> : IRepository<string, T>, IDisposable
                where T : class?, IComparable<T>, new()
            {
                public const int Limit = 10;
                public const decimal Rate = 1.5m;
                public const float Ratio = 0.25f;
                private static readonly List<T> _items = new();
                protected internal volatile int _hits;

                /// <summary>
                ///     Raised on change.
                /// </summary>
                public event EventHandler<T>? Changed;

                protected Repository(int capacity = 4, string? name = null, Level level = Level.High, ConsoleColor color = default) { }

                public abstract T Get(string key);
                public virtual int Count => 0;
                public static IRepository<string, T> Empty => null!;
                public required string Name { get; init; }
                public int this[int index] { get => 0; private set { } }
                public (int count, string) Stats { get; } = default;
                public Dictionary<string, List<T?>>? Cache { get; protected set; }
                int IRepository<string, T>.Count => 0;
                public unsafe int* Pointer => null;
                public nint Native => 0;
                public dynamic Dyn => 1;

                public static bool TryParse(ReadOnlySpan<char> text, out T? value, in int hint, ref readonly long big, scoped ref int r)
                {
                    value = null;
                    return false;
                }

                public async System.Threading.Tasks.Task RunAsync<TArg>(TArg arg, [CallerMemberName] string caller = "")
                    where TArg : struct
                {
                    await System.Threading.Tasks.Task.Yield();
                }
                public partial void OnLoaded();
                public partial void OnLoaded() { }
                public void Dispose() { }
                public static Repository<T> operator +(Repository<T> a, Repository<T> b) => a;
                public static implicit operator int(Repository<T> r) => 0;

                public delegate void Handler(T item);

                public readonly record struct Key(int Id, string Text);
                public record Entry(T Value) { public int Extra { get; init; } }
                public sealed class Nested<TInner> { public TInner? Value; }
                public enum State { Idle, Busy }
            }

            public ref struct Window<T> where T : unmanaged
            {
                public readonly ReadOnlySpan<T> Span;
                public Window(ReadOnlySpan<T> span) { Span = span; }
                public readonly int Length => Span.Length;
                public readonly void Touch() { }
            }

            public delegate ref readonly int Picker<T>(in T value, params int[] rest) where T : struct;

            public unsafe ref struct Slot<[Mark("param")] T>
                where T : unmanaged
            {
                public ref int Value;
                public ref readonly int Peek => ref Value;
                public ref int Borrow() => ref Value;
                public delegate* unmanaged[Cdecl]<int, void> Callback;
            }

            public readonly struct Frozen
            {
                public int Length => 0;
                public void Touch() { }
            }

            public sealed class TagAttribute<T> : Attribute
            {
                public TagAttribute(string label) { }
            }

            [Tag<int>("once")]
            public partial record Positional(int Id);

            public class Wrapper
            {
                public class Only { }
            }

            public interface IWatched
            {
                event EventHandler Changed;
                int Size { get; set; }
            }

            public sealed class Watched : IWatched
            {
                event EventHandler IWatched.Changed { add { } remove { } }
                public event EventHandler Ticked { add { } remove { } }
                int IWatched.Size { get; set; }
            }

            public partial class Marked<[Mark("param")] T> { }

            public sealed class ClashAttribute : Attribute
            {
                public ClashAttribute(string Value) { }
                public string? Value { get; set; }
            }

            [Mark("ext", codes: new[] { 7 })]
            [Mark(level: Level.High, name: "named")]
            [Tag<int>("tagged")]
            [Clash("from constructor", Value = "from property")]
            public static class Extensions
            {
                public static int Twice(this int x) => x * 2;
            }
        }
        """;

    private const string Header = "// <auto-generated/>\n\n#nullable enable";

    private static readonly ReadOptions _withDocs = new(IncludeDocumentationComments: true);

    private static CSharpCompilation Compilation => field ??= Compiling.AssertCompiles(FixtureSource);

    private static TypeDeclaration Repository => Compilation.Type("Fixture.Repository`1").ToDeclaration(_withDocs);

    [Test]
    public void ToDeclaration_Class_ReadsShapeWithoutPartial()
    {
        var declaration = Repository;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(declaration.TypeKind, Is.EqualTo(TypeKind.Class));
            Assert.That(declaration.IsRecord, Is.False);
            Assert.That(declaration.Accessibility, Is.EqualTo(Accessibility.Public));
            Assert.That(declaration.Modifiers, Is.EqualTo(Modifiers.Abstract), "partial is unknowable from a symbol");
            Assert.That(declaration.Name, Is.EqualTo("Repository"));
            Assert.That(declaration.BaseType, Is.Null);
            Assert.That(
                declaration.Interfaces.Select(i => i.ToString()),
                Is.EqualTo(new[] { "global::Fixture.IRepository<string, T>", "global::System.IDisposable" }));
        }
    }

    [Test]
    public void ToDeclaration_WithDocumentation_StripsTheMemberWrapper()
    {
        Assert.That(
            Repository.DocumentationComment,
            Is.EqualTo("<summary>\n    A repository.\n</summary>\n<typeparam name=\"T\">The item.</typeparam>"));
    }

    [Test]
    public void ToDeclaration_TypeParameter_ReadsConstraints()
    {
        var typeParameter = Repository.TypeParameters.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(typeParameter.HasReferenceTypeConstraint, Is.True);
            Assert.That(typeParameter.ReferenceTypeConstraintNullableAnnotation, Is.EqualTo(NullableAnnotation.Annotated));
            Assert.That(typeParameter.HasConstructorConstraint, Is.True);
            Assert.That(typeParameter.ConstraintTypes.Single().ToString(), Is.EqualTo("global::System.IComparable<T>"));
        }
    }

    [Test]
    public void ToDeclaration_Attribute_ReadsArgumentsWithTypesAsHoles()
    {
        var mark = Repository.Attributes.Single();

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
                }));
            Assert.That(
                mark.Arguments[3].Render(RenderOptions.Default with { Qualification = Qualification.Minimal }),
                Is.EqualTo("Kind = typeof(List<int>)"));
        }
    }

    [Test]
    public void ToDeclaration_Fields_ReadModifiersAndConstants()
    {
        var fields = Repository.Members.OfType<FieldDeclaration>().ToDictionary(f => f.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["Limit"].Modifiers, Is.EqualTo(Modifiers.Const));
            Assert.That(fields["Limit"].Initializer?.ToString(), Is.EqualTo("10"));
            Assert.That(fields["Rate"].Initializer?.ToString(), Is.EqualTo("1.5m"), "a decimal keeps its suffix, or it reads as a double");
            Assert.That(fields["Ratio"].Initializer?.ToString(), Is.EqualTo("0.25f"));
            Assert.That(fields["_items"].Modifiers, Is.EqualTo(Modifiers.Static | Modifiers.ReadOnly));
            Assert.That(fields["_items"].Type, Is.EqualTo(Types.List.Construct(Types.T)));
            Assert.That(fields["_hits"].Accessibility, Is.EqualTo(Accessibility.ProtectedOrInternal));
            Assert.That(fields["_hits"].Modifiers, Is.EqualTo(Modifiers.Volatile));
        }
    }

    [Test]
    public void ToDeclaration_Event_ReadsTypeAndDocumentation()
    {
        var changed = Repository.Members.OfType<EventDeclaration>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed.Type.ToString(), Is.EqualTo("global::System.EventHandler<T>?"));
            Assert.That(changed.DocumentationComment, Is.EqualTo("<summary>\n    Raised on change.\n</summary>"));
        }
    }

    [Test]
    public void ToDeclaration_Constructor_ReadsDefaultsAsExpressions()
    {
        var constructor = Repository.Members.OfType<ConstructorDeclaration>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(constructor.Accessibility, Is.EqualTo(Accessibility.Protected));
            Assert.That(
                constructor.Parameters.Select(p => p.Default?.ToString()),
                Is.EqualTo(new[] { "4", "null", "global::Fixture.Level.High", "global::System.ConsoleColor.Black" }),
                "an enum default names the member that has its value");
            Assert.That(constructor.Parameters[1].Type, Is.EqualTo(Types.Nullable(Types.String)));
            Assert.That(constructor.Body, Is.EqualTo(Snippet.Empty));
        }
    }

    [Test]
    public void ToDeclaration_Properties_ReadAccessorShapes()
    {
        var properties = Repository.Members.OfType<PropertyDeclaration>().Where(p => p.ExplicitInterfaceSpecifier is null).ToDictionary(p => p.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(properties["Count"].Modifiers, Is.EqualTo(Modifiers.Virtual));
            Assert.That(properties["Count"].Setter, Is.Null);
            Assert.That(properties["Count"].Getter, Is.EqualTo(new AccessorDeclaration()));
            Assert.That(properties["Name"].Modifiers, Is.EqualTo(Modifiers.Required));
            Assert.That(properties["Name"].Setter, Is.EqualTo(new AccessorDeclaration(IsInitOnly: true)));
            Assert.That(properties["this"].IsIndexer, Is.True);
            Assert.That(properties["this"].Setter!.Accessibility, Is.EqualTo(Accessibility.Private));
            Assert.That(properties["Cache"].Setter!.Accessibility, Is.EqualTo(Accessibility.Protected));
        }
    }

    [Test]
    public void ToDeclaration_Properties_ReadEveryKindOfType()
    {
        var properties = Repository.Members.OfType<PropertyDeclaration>().Where(p => p.ExplicitInterfaceSpecifier is null).ToDictionary(p => p.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(properties["Stats"].Type.ToString(), Is.EqualTo("(int count, string)"));
            Assert.That(
                properties["Cache"].Type.ToString(),
                Is.EqualTo("global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.List<T?>>?"));
            Assert.That(properties["Pointer"].Type, Is.EqualTo(new PointerTypeReference(PointedAtType: Types.Int32)));
            Assert.That(properties["Native"].Type.ToString(), Is.EqualTo("nint"));
            Assert.That(properties["Dyn"].Type, Is.EqualTo(new DynamicTypeReference()));
        }
    }

    [Test]
    public void ToDeclaration_ExplicitImplementation_HasSpecifierAndNoAccessibility()
    {
        var count = Repository.Members.OfType<PropertyDeclaration>().Single(p => p.ExplicitInterfaceSpecifier is not null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(count.Name, Is.EqualTo("Count"));
            Assert.That(count.Accessibility, Is.EqualTo(Accessibility.NotApplicable));
            Assert.That(count.ExplicitInterfaceSpecifier!.ToString(), Is.EqualTo("global::Fixture.IRepository<string, T>"));
        }
    }

    [Test]
    public void ToDeclaration_Methods_ReadParametersAndModifiers()
    {
        var methods = Repository.Members.OfType<MethodDeclaration>().ToList();
        var get = methods.Single(m => m.Name == "Get");
        var tryParse = methods.Single(m => m.Name == "TryParse");
        var run = methods.Single(m => m.Name == "RunAsync");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(get.Modifiers, Is.EqualTo(Modifiers.Abstract));
            Assert.That(get.Body, Is.Null);
            Assert.That(
                tryParse.Parameters.Select(p => p.RefKind),
                Is.EqualTo(new[] { RefKind.None, RefKind.Out, RefKind.In, RefKind.RefReadOnlyParameter, RefKind.Ref }));
            Assert.That(tryParse.Parameters[4].IsScoped, Is.True);
            Assert.That(tryParse.Parameters[1].Type, Is.EqualTo(Types.T with { NullableAnnotation = NullableAnnotation.Annotated }));
            Assert.That(run.Modifiers, Is.EqualTo(Modifiers.Async));
            Assert.That(run.TypeParameters.Single().HasValueTypeConstraint, Is.True);
            Assert.That(
                run.Parameters[1].Attributes.Single().Type.ToString(),
                Is.EqualTo("global::System.Runtime.CompilerServices.CallerMemberNameAttribute"));
            Assert.That(run.Parameters[1].Default?.ToString(), Is.EqualTo("\"\""));
        }
    }

    [Test]
    public void ToDeclaration_PartialAndOperatorMethods_ReadOnceAndByToken()
    {
        var methods = Repository.Members.OfType<MethodDeclaration>().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(methods.Count(m => m.Name == "OnLoaded"), Is.EqualTo(1), "a partial pair is one symbol");
            Assert.That(methods.Single(m => m.Name == "OnLoaded").Modifiers, Is.EqualTo(Modifiers.Partial));
            Assert.That(methods.Single(m => m.Name == "operator +").Modifiers, Is.EqualTo(Modifiers.Static));
            Assert.That(methods.Any(m => m.Name.Contains("implicit", System.StringComparison.Ordinal)), Is.False, "conversions have no typed declaration");
        }
    }

    [Test]
    public void ToDeclaration_NestedTypes_ReadRecordsDelegatesAndEnums()
    {
        var nested = Repository.Members.OfType<TypeDeclaration>().ToDictionary(t => t.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nested["Key"].TypeKind, Is.EqualTo(TypeKind.Struct));
            Assert.That(nested["Key"].IsRecord, Is.True);
            Assert.That(nested["Key"].Modifiers, Is.EqualTo(Modifiers.ReadOnly));
            Assert.That(nested["Key"].Members.OfType<ConstructorDeclaration>().Single().Parameters.Select(p => p.Name), Is.EqualTo(new[] { "Id", "Text" }));
            Assert.That(nested["Key"].Members.OfType<MethodDeclaration>(), Is.Empty, "synthesized record members are implicit");
            Assert.That(nested["Entry"].TypeKind, Is.EqualTo(TypeKind.Class));
            Assert.That(nested["Entry"].Members.OfType<PropertyDeclaration>().Select(p => p.Name), Is.EqualTo(new[] { "Value", "Extra" }));
            Assert.That(nested["Nested"].Members.OfType<FieldDeclaration>().Single().Type.ToString(), Is.EqualTo("TInner?"));
            Assert.That(nested["Handler"].TypeKind, Is.EqualTo(TypeKind.Delegate), "a nested delegate is a nested type like any other");
            Assert.That(nested["Handler"].ParameterList.Single().Type, Is.EqualTo(Types.T));
            Assert.That(nested["State"].Members.OfType<EnumMemberDeclaration>().Select(m => m.Value?.ToString()), Is.EqualTo(new[] { "0", "1" }));
        }
    }

    [Test]
    public void ToDeclaration_Enum_ReadsUnderlyingTypeAndValues()
    {
        var level = Compilation.Type("Fixture.Level").ToDeclaration();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(level.EnumUnderlyingType, Is.EqualTo(Types.Byte));
            Assert.That(
                level.Members.OfType<EnumMemberDeclaration>().Select(m => (m.Name, m.Value?.ToString())),
                Is.EqualTo(new[] { ("Low", "0"), ("High", "5") }));
        }
    }

    [Test]
    public void ToDeclaration_Interface_DropsRedundantModifiers()
    {
        var declaration = Compilation.Type("Fixture.IRepository`2").ToDeclaration();
        var get = declaration.Members.OfType<MethodDeclaration>().Single();
        var empty = declaration.Members.OfType<PropertyDeclaration>().Single(p => p.Name == "Empty");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(declaration.TypeParameters.Select(t => t.Variance), Is.EqualTo(new[] { VarianceKind.In, VarianceKind.Out }));
            Assert.That(declaration.TypeParameters[0].HasNotNullConstraint, Is.True);
            Assert.That(get.Accessibility, Is.EqualTo(Accessibility.NotApplicable));
            Assert.That(get.Modifiers, Is.EqualTo(Modifiers.None));
            Assert.That(empty.Modifiers, Is.EqualTo(Modifiers.Static | Modifiers.Abstract));
        }
    }

    [Test]
    public void ToDeclaration_ReadOnlyStructMembers_CarryReadOnlyOnlyWhereWritten()
    {
        var window = Compilation.Type("Fixture.Window`1").ToDeclaration();
        var frozen = Compilation.Type("Fixture.Frozen").ToDeclaration();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(window.Modifiers, Is.EqualTo(Modifiers.Ref));
            Assert.That(window.TypeParameters.Single().HasUnmanagedTypeConstraint, Is.True);
            Assert.That(window.Members.OfType<PropertyDeclaration>().Single().Modifiers, Is.EqualTo(Modifiers.ReadOnly));
            Assert.That(window.Members.OfType<MethodDeclaration>().Single().Modifiers, Is.EqualTo(Modifiers.ReadOnly));
            Assert.That(frozen.Modifiers, Is.EqualTo(Modifiers.ReadOnly));
            Assert.That(frozen.Members.OfType<PropertyDeclaration>().Single().Modifiers, Is.EqualTo(Modifiers.None), "implied by the struct");
            Assert.That(frozen.Members.OfType<MethodDeclaration>().Single().Modifiers, Is.EqualTo(Modifiers.None));
        }
    }

    [Test]
    public void ToDeclaration_ExtensionMethod_MarksTheReceiver()
    {
        var twice = Compilation.Type("Fixture.Extensions").ToDeclaration().Members.OfType<MethodDeclaration>().Single();

        Assert.That(twice.Parameters.Single().IsThis, Is.True);
    }

    [Test]
    public void ToDeclaration_Delegate_ReadsSignatureAndNoMembers()
    {
        var picker = Compilation.Type("Fixture.Picker`1").ToDeclaration();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(picker.TypeKind, Is.EqualTo(TypeKind.Delegate));
            Assert.That(picker.RefKind, Is.EqualTo(RefKind.RefReadOnly));
            Assert.That(picker.ReturnType, Is.EqualTo(Types.Int32));
            Assert.That(picker.ParameterList.Select(p => (p.RefKind, p.IsParams)), Is.EqualTo(new[] { (RefKind.In, false), (RefKind.None, true) }));
            Assert.That(picker.Members.IsEmpty, Is.True);
            Assert.That(picker.TypeParameters[0].HasValueTypeConstraint, Is.True);
        }
    }

    [Test]
    public void ToDeclaration_RefMembersAndFunctionPointers_ReadRefKinds()
    {
        var slot = Compilation.Type("Fixture.Slot`1").ToDeclaration();
        var value = slot.Members.OfType<FieldDeclaration>().Single(f => f.Name == "Value");
        var callback = (FunctionPointerTypeReference)slot.Members.OfType<FieldDeclaration>().Single(f => f.Name == "Callback").Type;
        var peek = slot.Members.OfType<PropertyDeclaration>().Single();
        var borrow = slot.Members.OfType<MethodDeclaration>().Single(m => m.Name == "Borrow");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(slot.TypeParameters[0].Attributes, Has.Length.EqualTo(1), "type parameter attributes are read");
            Assert.That(value.RefKind, Is.EqualTo(RefKind.Ref));
            Assert.That(peek.RefKind, Is.EqualTo(RefKind.RefReadOnly));
            Assert.That(borrow.RefKind, Is.EqualTo(RefKind.Ref));
            Assert.That(callback.CallingConvention, Is.EqualTo(SignatureCallingConvention.CDecl), "a single well-known convention comes back through the enum");
            Assert.That(callback.Parameters, Has.Length.EqualTo(1));
            Assert.That(callback.ToString(), Is.EqualTo("delegate* unmanaged[Cdecl]<int, void>"));
        }
    }

    [Test]
    public void ToDeclaration_SameSourceTwice_IsEqual()
    {
        var first = Compiling.AssertCompiles(FixtureSource).Type("Fixture.Repository`1").ToDeclaration(_withDocs);
        var second = Compiling.AssertCompiles(FixtureSource).Type("Fixture.Repository`1").ToDeclaration(_withDocs);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }
    }

    [Test]
    public void ToDeclaration_Nested_ReadsTheContainingShape()
    {
        var nested = Compilation.Type("Fixture.Repository`1+Nested`1").ToDeclaration();
        var wrapper = Compilation.Type("Fixture.Wrapper").ToDeclaration();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wrapper.ContainingType, Is.Null, "a top-level type is in nothing");
            Assert.That(nested.ContainingType!.Name, Is.EqualTo("Repository"));
            Assert.That(nested.ContainingType.Modifiers, Is.EqualTo(Modifiers.Partial));
            Assert.That(nested.ContainingType.TypeParameters.Single().Name, Is.EqualTo("T"), "with the type parameters a part repeats");
            Assert.That(nested.Members.OfType<FieldDeclaration>().Single().Name, Is.EqualTo("Value"), "the type keeps its members");
        }
    }

    [Test]
    public void ToFile_NestedType_RendersThePartAroundIt()
    {
        var nested = Compilation.Type("Fixture.Repository`1+Nested`1");
        var unit = nested.ToFile();
        unit = unit with
        {
            Members = [((NamespaceDeclaration)unit.Members[0]) with
            {
                Members = [((TypeDeclaration)((NamespaceDeclaration)unit.Members[0]).Members[0]).AddMembers(new FieldDeclaration(Type: Types.Int32, Name: "x"))],
            }],
        };

        var text = unit.Render();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unit.HintName, Is.EqualTo("Fixture.Repository`1.Nested`1.g.cs"));
            Assert.That(text, Does.Contain("partial class Repository<T>\n{\n"));
            Assert.That(text, Does.Contain("        int x;\n"));
        }
    }

    [Test]
    public void ToDeclaration_Namespace_IsEmptyAndTheGlobalOneHasNoName()
    {
        var ns = Compilation.Type("Fixture.Repository`1").ContainingNamespace.ToDeclaration();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ns.Name, Is.EqualTo("Fixture"));
            Assert.That(ns.Members.IsEmpty, Is.True);
            Assert.That(Compilation.GlobalNamespace.ToDeclaration().Name, Is.Empty);
        }
    }

    [Test]
    public void ToDeclaration_ShapeWithPartial_CompilesAsANewPart()
    {
        var type = Compilation.Type("Fixture.Repository`1");
        var shape = type.ToDeclaration(ReadOptions.Shape);
        var part = shape with
        {
            Modifiers = shape.Modifiers | Modifiers.Partial,
            Members = [new MethodDeclaration(Accessibility: Accessibility.Public, ReturnType: Types.Void, Name: "Touch", Body: Snippet.Empty)],
        };
        var unit = new CompilationUnit(Members: [type.ContainingNamespace.ToDeclaration() with { Members = [part] }], Header: Header);

        Assert.That(unit.HintName, Is.EqualTo("Fixture.Repository`1.g.cs"));
        Compiling.AssertCompiles(FixtureSource, LanguageVersion.Latest, unit.Render());
    }

    [Test]
    public void ToDeclaration_WithAttributesAsAPart_IsDuplicateAttribute()
    {
        // A symbol does not know which constructor was primary, and the reader never fills ParameterList for a record, so the
        // only thing a careless part repeats is the attribute.
        var positional = Compilation.Type("Fixture.Positional");
        var shape = positional.ToDeclaration(new ReadOptions(IncludeMembers: false));
        var unit = new CompilationUnit(Members: [positional.ContainingNamespace.ToDeclaration() with
        {
            Members = [shape with { Modifiers = shape.Modifiers | Modifiers.Partial }],
        }]);

        var errors = Compiling.Compile(FixtureSource, LanguageVersion.Latest, unit.Render()).GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.Id)
            .Distinct()
            .ToList();

        Assert.That(errors, Is.EqualTo(new[] { "CS0579" }));
    }

    [Test]
    public void ToDeclaration_WithoutAttributes_LeavesThemOffTypeParametersToo()
    {
        var marked = Compilation.Type("Fixture.Marked`1");
        var shape = marked.ToDeclaration(ReadOptions.Shape);
        var unit = new CompilationUnit(Members: [marked.ContainingNamespace.ToDeclaration() with
        {
            Members = [shape with { Modifiers = shape.Modifiers | Modifiers.Partial }],
        }]);

        var part = unit.Render();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(part, Does.Contain("partial class Marked<T>"));
            Assert.That(part, Does.Not.Contain("MarkAttribute"));
            Assert.That(marked.ToDeclaration().TypeParameters[0].Attributes, Is.Not.Empty, "reading the type itself still gives them");
        }

        Compiling.AssertCompiles(FixtureSource, LanguageVersion.Latest, part);
    }

    [Test]
    public void ToDeclaration_ReadOptions_DecideHowMuchIsRead()
    {
        var key = Compilation.Type("Fixture.Repository`1+Key");
        var whole = key.ToDeclaration();
        var withImplicit = key.ToDeclaration(new ReadOptions(IncludeImplicitlyDeclared: true));
        var shape = key.ToDeclaration(new ReadOptions(IncludeMembers: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whole.Members.OfType<MethodDeclaration>(), Is.Empty, "a record's synthesized members are implicit");
            Assert.That(withImplicit.Members.OfType<MethodDeclaration>().Select(m => m.Name), Does.Contain("ToString"));
            Assert.That(shape.Members.IsEmpty, Is.True);
            Assert.That(whole.DocumentationComment, Is.Null, "documentation is off unless asked for");
        }
    }

    [Test]
    public void Render_ReadDeclarationsWithBodiesAdded_CompilesAgain()
    {
        var unit = new CompilationUnit(
            Members: [
                new NamespaceDeclaration(
                    Name: "Fixture",
                    Members: [
                        Compilation.Type("Fixture.Level").ToDeclaration(_withDocs),
                        Compilation.Type("Fixture.MarkAttribute").ToDeclaration(_withDocs),
                        Compilation.Type("Fixture.IRepository`2").ToDeclaration(_withDocs),
                        Compilation.Type("Fixture.Window`1").ToDeclaration(_withDocs),
                    ]),
            ],
            Header: Header);

        // Members read from symbols carry no body. The caller supplies them.
        var fixture = (NamespaceDeclaration)unit.Members.Single();
        unit = unit with
        {
            Members = [fixture with
            {
                Members = fixture.Members.OfType<TypeDeclaration>().Select(type => (MemberDeclaration)(type with
                {
                    Members = type.Members.Select(member => member switch
                    {
                        ConstructorDeclaration c => c with { Body = "_ = 0;" },
                        MethodDeclaration method when (method.Modifiers & (Modifiers.Abstract | Modifiers.Partial | Modifiers.Extern)) == 0
                            && type.TypeKind != TypeKind.Interface => method with { Body = Snippet.Empty },
                        _ => member,
                    }).ToEquatableArray(),
                })).ToEquatableArray(),
            }],
        };

        var text = unit.Render();
        Compiling.AssertCompiles(text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("public enum Level : byte\n{\n    Low = 0,\n    High = 5,\n}"));
            Assert.That(text, Does.Contain("public interface IRepository<in TKey, out TValue> where TKey : notnull"));
            Assert.That(text, Does.Contain("static abstract global::Fixture.IRepository<TKey, TValue> Empty { get; }"));
            Assert.That(text, Does.Contain("public ref struct Window<T> where T : unmanaged"));
            Assert.That(text, Does.Contain("public readonly int Length { get; }"));
            Assert.That(text, Does.Contain("public MarkAttribute(string name, global::Fixture.Level level = global::Fixture.Level.Low, params int[] codes)"));
        }
    }

    [Test]
    public void Render_ReadDelegate_CompilesAsADelegate()
    {
        string text = Compilation.Type("Fixture.Picker`1").ToFile().Render();

        Assert.That(text, Does.Contain("public delegate ref readonly int Picker<T>(in T value, params int[] rest)"));
        Compiling.AssertCompiles(text);
    }

    [Test]
    public void Render_KeywordNames_EscapeEverywhere()
    {
        const string source = """
            namespace @event
            {
                public class @class<@int>
                {
                    public int @void;
                    public string @string { get; set; }
                    public void @if(int @out, string @this) { }
                    public event System.Action @checked;
                    public (int @base, string @lock) @operator() => default;
                }
            }
            """;

        string rendered = Compiling.Compile(source).Type("event.class`1").ToFile().Render();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rendered, Does.Contain("namespace @event;"));
            Assert.That(rendered, Does.Contain("public class @class<@int>"));
            Assert.That(rendered, Does.Contain("public int @void;"));
            Assert.That(rendered, Does.Contain("public string @string"));
            Assert.That(rendered, Does.Contain("public void @if(int @out, string @this)"));
            Assert.That(rendered, Does.Contain("public event global::System.Action @checked;"));
            Assert.That(rendered, Does.Contain("public (int @base, string @lock) @operator()"));
        }
    }

    [Test]
    public void ToSourceText_File_IsUtf8UnderTheOptionsGiven()
    {
        var text = Compilation.Type("Fixture.Level").ToFile().ToSourceText(RenderOptions.Default with { Indent = "\t" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text.Encoding, Is.EqualTo(Encoding.UTF8), "AddSource needs an encoding to hash");
            Assert.That(text.ToString(), Does.Contain("\tLow = 0,"));
        }
    }

    [Test]
    public void ToDeclaration_PointerMembers_CarryUnsafe()
    {
        var pointer = Repository.Members.OfType<PropertyDeclaration>().Single(p => p.Name == "Pointer");
        var callback = Compilation.Type("Fixture.Slot`1").ToDeclaration().Members.OfType<FieldDeclaration>()
            .Single(f => f.Name == "Callback");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pointer.Modifiers, Is.EqualTo(Modifiers.Unsafe), "a symbol does not report the keyword, the pointer does");
            Assert.That(callback.Modifiers, Is.EqualTo(Modifiers.Unsafe));
            Assert.That(
                new CompilationUnit(Members: [new TypeDeclaration(Name: "C", Members: [pointer])], Header: Header).Render(),
                Does.Contain("public unsafe int* Pointer"));
        }
    }

    [Test]
    public void ToDeclaration_EventsWithAccessors_ReadThemAsShape()
    {
        var events = Compilation.Type("Fixture.Watched").ToDeclaration().Members.OfType<EventDeclaration>().ToDictionary(e => e.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(events["Ticked"].Adder, Is.EqualTo(new AccessorDeclaration()));
            Assert.That(events["Ticked"].Remover, Is.EqualTo(new AccessorDeclaration()));
            Assert.That(events["Ticked"].ExplicitInterfaceSpecifier, Is.Null);
            Assert.That(events["Changed"].Name, Is.EqualTo("Changed"), "the interface is not part of the name");
            Assert.That(events["Changed"].ExplicitInterfaceSpecifier?.ToString(), Is.EqualTo("global::Fixture.IWatched"));
            Assert.That(events["Changed"].Accessibility, Is.EqualTo(Accessibility.NotApplicable));
            Assert.That(Repository.Members.OfType<EventDeclaration>().Single().Adder, Is.Null, "a field-like event has no accessors of its own");
        }
    }

    [Test]
    public void ToDeclaration_ExplicitInterfaceProperty_LeavesItsAccessorsUnmodified()
    {
        var size = Compilation.Type("Fixture.Watched").ToDeclaration().Members.OfType<PropertyDeclaration>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(size.Accessibility, Is.EqualTo(Accessibility.NotApplicable));
            Assert.That(size.ExplicitInterfaceSpecifier?.ToString(), Is.EqualTo("global::Fixture.IWatched"));
            Assert.That(size.Getter!.Accessibility, Is.EqualTo(Accessibility.NotApplicable), "the symbol says private, C# forbids saying so");
            Assert.That(size.Setter!.Accessibility, Is.EqualTo(Accessibility.NotApplicable));
        }
    }

    [Test]
    public void ToDeclaration_EventFromMetadata_IsFieldLike()
    {
        var changed = Compilation.Type("System.ComponentModel.INotifyPropertyChanged").ToDeclaration().Members.OfType<EventDeclaration>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed.Adder, Is.Null, "metadata cannot say whether the accessors were written, and only a field-like event renders without bodies");
            Assert.That(changed.Remover, Is.Null);
        }
    }

    [Test]
    public void Render_ReadEventsWithBodiesAdded_CompileAgain()
    {
        var watched = Compilation.Type("Fixture.Watched").ToDeclaration();
        var withBodies = watched with
        {
            Members = [.. watched.Members.Select(m => m is EventDeclaration e
                ? e with { Adder = new AccessorDeclaration(Body: Snippet.Empty), Remover = new AccessorDeclaration(Body: Snippet.Empty) }
                : m)],
        };
        var unit = new CompilationUnit(Members: [new NamespaceDeclaration(Name: "Fixture", Members: [withBodies])], Header: Header);

        var text = unit.Render();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("event global::System.EventHandler global::Fixture.IWatched.Changed"));
            Assert.That(text, Does.Contain("int global::Fixture.IWatched.Size { get; set; }"));
        }

        Compiling.AssertCompiles(
            text,
            LanguageVersion.Latest,
            "namespace Fixture { public interface IWatched { event System.EventHandler Changed; int Size { get; set; } } }");
    }
}

using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace Redeclare.Tests;

/// <summary>
///     Covers the walk behind <see cref="CSharpRenderer.CollectNamespaces"/>, which a generator calls to learn
///     which usings its file needs before rendering under <see cref="Qualification.Minimal"/>. A type hiding in a
///     place the walk forgets is a missing using, which the consumer sees as a compile error.
/// </summary>
[TestFixture]
public sealed class CollectNamespacesTests
{
    private static readonly NamedTypeReference _uri = new(Name: "Uri", ContainingNamespace: "System");

    private static readonly NamedTypeReference _guid = new(Name: "Guid", ContainingNamespace: "System", TypeKind: TypeKind.Struct);

    private static readonly NamedTypeReference _regex = new(Name: "Regex", ContainingNamespace: "System.Text.RegularExpressions");

    private static string[] Collect(params MemberDeclaration[] members)
    {
        return [.. CSharpRenderer.CollectNamespaces(new CompilationUnit(Members: members), RenderOptions.Default)];
    }

    [Test]
    public void CollectNamespaces_EveryMemberKind_FindsTheTypesItHolds()
    {
        var namespaces = Collect(new NamespaceDeclaration(
            Name: "Own",
            Members: [
                new TypeDeclaration(
                    Name: "C",
                    Members: [
                        new MethodDeclaration(ReturnType: Types.Stream, Name: "Open"),
                        new ConstructorDeclaration(Body: Snippet.Empty, Parameters: [new ParameterDeclaration(Type: _uri, Name: "uri")]),
                        new PropertyDeclaration(Type: Types.StringBuilder, Name: "Text", Getter: new AccessorDeclaration(), Setter: null),
                        new FieldDeclaration(Type: _guid, Name: "_id"),
                        new EventDeclaration(Type: Types.EventHandler, Name: "Changed"),
                        new RawMemberDeclaration(Text: Snippet.From($"private {_regex}? _pattern;")),
                    ]),
                new TypeDeclaration(
                    Name: "E",
                    TypeKind: TypeKind.Enum,
                    Members: [new EnumMemberDeclaration(Name: "One", Value: Snippet.From($"(int){Types.Byte}.MaxValue"))]),
            ]));

        Assert.That(
            namespaces,
            Is.EqualTo(new[] { "System", "System.IO", "System.Text", "System.Text.RegularExpressions" }));
    }

    [Test]
    public void CollectNamespaces_TypesInsideTypes_FindEveryPart()
    {
        var namespaces = Collect(new TypeDeclaration(
            Name: "C",
            Members: [
                new FieldDeclaration(Type: new ArrayTypeReference(ElementType: _uri), Name: "_uris"),
                new FieldDeclaration(Type: new PointerTypeReference(PointedAtType: _guid), Name: "_id"),
                new FieldDeclaration(Type: Types.List.Construct(_regex), Name: "_patterns"),
                new FieldDeclaration(
                    Type: new TupleTypeReference(Elements: [new TupleElement(Type: Types.Stream), new TupleElement(Type: Types.Int32)]),
                    Name: "_pair"),
                new FieldDeclaration(
                    Type: new FunctionPointerTypeReference(
                        ReturnType: Types.Void,
                        Parameters: [new FunctionPointerParameter(Type: Types.StringBuilder)]),
                    Name: "_callback"),
            ]));

        Assert.That(
            namespaces,
            Is.EqualTo(new[] { "System", "System.Collections.Generic", "System.IO", "System.Text", "System.Text.RegularExpressions" }));
    }

    [Test]
    public void CollectNamespaces_NestedType_TakesTheNamespaceOfItsOutermostContainer()
    {
        var inner = new NamedTypeReference(
            Name: "Enumerator",
            ContainingType: new NamedTypeReference(Name: "List", ContainingNamespace: "System.Collections.Generic", Arity: 1),
            TypeKind: TypeKind.Struct);

        var namespaces = Collect(new TypeDeclaration(Name: "C", Members: [new FieldDeclaration(Type: inner, Name: "_e")]));

        Assert.That(namespaces, Is.EqualTo(new[] { "System.Collections.Generic" }));
    }

    [Test]
    public void CollectNamespaces_AttributesAnywhere_Count()
    {
        var attribute = new AttributeSpecification(Type: Types.SerializableAttribute, Arguments: [Snippet.From($"typeof({_regex})")]);
        var namespaces = Collect(new TypeDeclaration(
            Name: "C",
            Attributes: [attribute],
            TypeParameters: [new TypeParameterDeclaration(Name: "T", Attributes: [new AttributeSpecification(Type: _uri)])],
            Members: [
                new MethodDeclaration(
                    ReturnType: Types.Void,
                    Name: "M",
                    Parameters: [new ParameterDeclaration(Type: Types.Int32, Name: "i", Attributes: [new AttributeSpecification(Type: _guid)])]),
            ]));

        Assert.That(namespaces, Is.EqualTo(new[] { "System", "System.Text.RegularExpressions" }));
    }

    [Test]
    public void CollectNamespaces_SnippetHolesInEveryBody_Count()
    {
        var hole = Snippet.From($"_ = typeof({_regex});");
        var namespaces = Collect(new TypeDeclaration(
            Name: "C",
            Members: [
                new MethodDeclaration(ReturnType: Types.Void, Name: "M", Body: hole),
                new ConstructorDeclaration(Body: hole, Initializer: Snippet.From($"this(default({_guid}))")),
                new PropertyDeclaration(
                    Type: Types.Int32,
                    Name: "P",
                    Getter: new AccessorDeclaration(Body: Snippet.From($"default({_uri}) is null ? 1 : 0")),
                    Setter: new AccessorDeclaration(Body: hole),
                    Initializer: Snippet.From($"default({Types.Int64}) is 0 ? 1 : 0")),
                new EventDeclaration(
                    Type: Types.EventHandler,
                    Name: "Changed",
                    Adder: new AccessorDeclaration(Body: Snippet.From($"_ = typeof({Types.Stream});")),
                    Remover: new AccessorDeclaration(Body: hole)),
            ]));

        Assert.That(
            namespaces,
            Is.EqualTo(new[] { "System", "System.IO", "System.Text.RegularExpressions" }));
    }

    [Test]
    public void CollectNamespaces_ExtensionBlock_FindsReceiverAndMembers()
    {
        var namespaces = Collect(new TypeDeclaration(
            Name: "Extensions",
            Modifiers: Modifiers.Static,
            Members: [
                new ExtensionDeclaration(
                    Receiver: new ParameterDeclaration(Type: _uri, Name: "uri"),
                    TypeParameters: [new TypeParameterDeclaration(Name: "T", ConstraintTypes: [Types.IDisposable])],
                    Members: [new MethodDeclaration(ReturnType: _regex, Name: "Pattern")]),
            ]));

        Assert.That(namespaces, Is.EqualTo(new[] { "System", "System.Text.RegularExpressions" }));
    }

    [Test]
    public void CollectNamespaces_ConstraintsAndExplicitInterfaces_Count()
    {
        var namespaces = Collect(new TypeDeclaration(
            Name: "C",
            TypeParameters: [new TypeParameterDeclaration(Name: "T", ConstraintTypes: [Types.IDisposable])],
            Members: [
                new MethodDeclaration(
                    ReturnType: Types.Void,
                    Name: "Dispose",
                    ExplicitInterfaceSpecifier: Types.IDisposable,
                    TypeParameters: [new TypeParameterDeclaration(Name: "U", ConstraintTypes: [_regex])]),
                new PropertyDeclaration(
                    Type: Types.Int32,
                    Name: "this",
                    Getter: new AccessorDeclaration(),
                    Setter: null,
                    Parameters: [new ParameterDeclaration(Type: _guid, Name: "id")],
                    ExplicitInterfaceSpecifier: Types.List.Construct(_uri)),
            ]));

        Assert.That(
            namespaces,
            Is.EqualTo(new[] { "System", "System.Collections.Generic", "System.Text.RegularExpressions" }));
    }

    [Test]
    public void CollectNamespaces_TypesWithAKeyword_AreLeftOut()
    {
        var namespaces = Collect(new TypeDeclaration(
            Name: "C",
            Members: [
                new FieldDeclaration(Type: Types.Int32, Name: "_i"),
                new FieldDeclaration(Type: Types.String, Name: "_s"),
                new FieldDeclaration(Type: Types.Object, Name: "_o"),
                new FieldDeclaration(Type: new DynamicTypeReference(), Name: "_d"),
                new FieldDeclaration(Type: new TypeParameterReference(Name: "T"), Name: "_t"),
                new FieldDeclaration(Type: new ErrorTypeReference(Text: "Missing"), Name: "_e"),
            ]));

        Assert.That(namespaces, Is.Empty);
    }

    [Test]
    public void CollectNamespaces_KeywordsTurnedOff_CountsSystem()
    {
        var unit = new CompilationUnit(Members: [new TypeDeclaration(Name: "C", Members: [new FieldDeclaration(Type: Types.String, Name: "_s")])]);
        var spelledOut = RenderOptions.Default with { PredefinedTypeKeywords = false };

        Assert.That(
            CSharpRenderer.CollectNamespaces(unit, spelledOut),
            Is.EqualTo(new[] { "System" }),
            "String is written without its keyword, so it needs its namespace");
    }

    [Test]
    public void CollectNamespaces_ContainingTypeConstraint_Counts()
    {
        var outer = new TypeDeclaration(
            Name: "Outer",
            TypeParameters: [new TypeParameterDeclaration(Name: "T", ConstraintTypes: [Types.IDisposable])]);

        var namespaces = Collect(new TypeDeclaration(Name: "Inner", ContainingType: outer));

        Assert.That(namespaces, Is.EqualTo(new[] { "System" }), "the part repeats the constraint, so the using is needed");
    }
}

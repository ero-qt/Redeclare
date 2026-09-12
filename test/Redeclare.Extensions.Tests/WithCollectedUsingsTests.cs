using NUnit.Framework;

namespace Redeclare.Extensions.Tests;

[TestFixture]
public sealed class WithCollectedUsingsTests
{
    private static readonly NamedTypeReference _stream = new(Name: "Stream", ContainingNamespace: "System.IO");

    private static readonly NamedTypeReference _builder = new(Name: "StringBuilder", ContainingNamespace: "System.Text");

    private static CompilationUnit Unit => new(
        Members: [
            new NamespaceDeclaration(
                Name: "Own",
                Members: [
                    new TypeDeclaration(
                        Name: "C",
                        Members: [
                            new FieldDeclaration(Type: _stream, Name: "_stream"),
                            new MethodDeclaration(ReturnType: _builder, Name: "Build"),
                            new FieldDeclaration(Type: TypeReference.Int32, Name: "_count"),
                        ]),
                ]),
        ]);

    [Test]
    public void WithCollectedUsings_File_TakesTheNamespacesItsTypesNeed()
    {
        Assert.That(
            Unit.WithCollectedUsings(RenderOptions.Default).Usings.ToArray(),
            Is.EqualTo(new[] { "System.IO", "System.Text" }),
            "the types contribute, and the namespace the file declares is not one of them");
    }

    [Test]
    public void WithCollectedUsings_TypesWithAKeyword_AreLeftOut()
    {
        Assert.That(Unit.WithCollectedUsings(RenderOptions.Default).Usings, Has.No.Member("System"), "int needs no using");
    }

    [Test]
    public void WithCollectedUsings_MinimalQualification_RendersShortNamesThatResolve()
    {
        var text = Unit.WithCollectedUsings(RenderOptions.Default).Render(RenderOptions.Default with { Qualification = Qualification.Minimal });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("using System.IO;"));
            Assert.That(text, Does.Contain("Stream _stream;"));
            Assert.That(text, Does.Not.Contain("global::"));
        }
    }

    [Test]
    public void WithCollectedUsings_FileThatCarriedUsings_ReplacesThem()
    {
        var carried = Unit with { Usings = ["System.Linq"] };

        Assert.That(carried.WithCollectedUsings(RenderOptions.Default).Usings, Has.No.Member("System.Linq"));
    }
}

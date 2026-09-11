using NUnit.Framework;
using System.Linq;

namespace Redeclare.Extensions.Tests;

[TestFixture]
public sealed class ToPartTests
{
    private const string Source = """
        namespace Shapes
        {
            public abstract partial class Outer<T> where T : struct
            {
                [System.Obsolete] public sealed partial class Inner
                {
                    public int Kept;
                }
            }
        }
        """;

    private static TypeDeclaration Inner => Compiling.Compile(Source).Type("Shapes.Outer`1+Inner").ToPart();

    [Test]
    public void ToPart_NestedType_IsPartialWithNothingOfItsOwn()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Inner.Modifiers.HasFlag(Modifiers.Partial), Is.True);
            Assert.That(Inner.Members.IsEmpty, Is.True, "a second part declares no members it does not add");
            Assert.That(Inner.Attributes.IsEmpty, Is.True, "repeating an attribute is CS0579");
        }
    }

    [Test]
    public void ToPart_NestedType_KeepsWhatAPartMustRepeat()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Inner.Name, Is.EqualTo("Inner"));
            Assert.That(Inner.Modifiers.HasFlag(Modifiers.Sealed), Is.True);
            Assert.That(Inner.ContainingType!.Name, Is.EqualTo("Outer"));
            Assert.That(Inner.ContainingType.TypeParameters.Single().Name, Is.EqualTo("T"));
        }
    }

    [Test]
    public void ToPart_AddingAMember_RendersAsAPart()
    {
        var part = Inner.AddMembers(new MethodDeclaration(ReturnType: TypeReference.Void, Name: "Touch", Body: Snippet.Empty));
        var unit = new CompilationUnit(Members: [new NamespaceDeclaration(Name: "Shapes", Members: [part])]);

        var text = unit.Render();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("partial class Outer<T>"), "the containing part comes along");
            Assert.That(text, Does.Contain("sealed partial class Inner"));
            Assert.That(text, Does.Contain("void Touch()"));
        }
    }
}

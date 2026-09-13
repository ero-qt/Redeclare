using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace Redeclare.Extensions.Tests;

[TestFixture]
public sealed class WithBodiesTests
{
    private static readonly PropertyDeclaration _definition = new(
        Accessibility: Accessibility.Public,
        Modifiers: Modifiers.Partial,
        Type: TypeReference.Int32,
        Name: "Count",
        Getter: new AccessorDeclaration(),
        Setter: new AccessorDeclaration(Accessibility: Accessibility.Private, IsInitOnly: true));

    [Test]
    public void WithBodies_BothAccessors_KeepTheirAccessibilityAndInit()
    {
        var implemented = _definition.WithBodies(getter: Snippet.Expression("_count"), setter: "_count = value;");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(implemented.Getter!.Body, Is.EqualTo(Snippet.Expression("_count")));
            Assert.That(implemented.Setter!.Body, Is.EqualTo((Snippet)"_count = value;"));
            Assert.That(implemented.Setter.Accessibility, Is.EqualTo(Accessibility.Private));
            Assert.That(implemented.Setter.IsInitOnly, Is.True);
            Assert.That(implemented with { Getter = _definition.Getter, Setter = _definition.Setter }, Is.EqualTo(_definition), "nothing else moved");
        }
    }

    [Test]
    public void WithBodies_NullBody_LeavesThatAccessorAlone()
    {
        var implemented = _definition.WithBodies(getter: Snippet.Expression("_count"));

        Assert.That(implemented.Setter, Is.EqualTo(_definition.Setter));
    }

    [Test]
    public void WithBodies_BodyForAnAccessorThatIsNotThere_IsIgnored()
    {
        var getterOnly = _definition with { Setter = null };

        Assert.That(getterOnly.WithBodies(setter: "_count = value;").Setter, Is.Null);
    }
}

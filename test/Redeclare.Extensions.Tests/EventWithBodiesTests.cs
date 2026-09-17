using NUnit.Framework;

namespace Redeclare.Extensions.Tests;

[TestFixture]
public sealed class EventWithBodiesTests
{
    private static readonly EventDeclaration _fieldLike = new(Type: TypeReference.Object, Name: "Changed");

    [Test]
    public void WithBodies_FieldLikeEvent_GainsAccessorsWithTheBodies()
    {
        var implemented = _fieldLike.WithBodies(add: "_changed += value;", remove: "_changed -= value;");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(implemented.Accessors!.Add.Body, Is.EqualTo((Snippet)"_changed += value;"));
            Assert.That(implemented.Accessors.Remove.Body, Is.EqualTo((Snippet)"_changed -= value;"));
        }
    }

    [Test]
    public void WithBodies_NullBody_LeavesThatAccessorAlone()
    {
        var implemented = _fieldLike.WithBodies(add: "_changed += value;");

        Assert.That(implemented.Accessors!.Remove, Is.EqualTo(EventAccessors.Auto.Remove));
    }
}

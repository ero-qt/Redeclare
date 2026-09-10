using NUnit.Framework;
using System.Linq;
using System.Collections.Generic;

namespace Redeclare.Tests;

[TestFixture]
public sealed class EquatableArrayTests
{
    [Test]
    public void Default_Always_IsEmptyAndEqualsEmpty()
    {
        EquatableArray<int> a = default;
        EquatableArray<int> b = [];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a.IsEmpty, Is.True);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Has.Length.EqualTo(0));
        }
    }

    [Test]
    public void Equals_SameContents_IsTrueAndHashesAlike()
    {
        EquatableArray<string> a = ["x", "y"];
        var b = new List<string> { "x", "y" }.ToEquatableArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }

    [Test]
    public void Equals_DifferentContents_IsFalse()
    {
        EquatableArray<string> a = ["x", "y"];
        EquatableArray<string> b = ["y", "x"];

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void ImplicitConversion_FromArray_Copies()
    {
        var source = new[] { 1, 2, 3 };
        EquatableArray<int> array = source;
        source[0] = 99;

        Assert.That(array[0], Is.EqualTo(1));
    }

    [Test]
    public void Add_Always_ReturnsNewArrayLeavingOriginal()
    {
        EquatableArray<int> a = [1];
        var b = a.Add(2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Has.Length.EqualTo(1));
            Assert.That(b, Is.EqualTo((EquatableArray<int>)[1, 2]));
        }
    }

    [Test]
    public void Read_ThroughCollectionApis_YieldsItems()
    {
        EquatableArray<int> array = [1, 2, 3];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(array.Count, Is.EqualTo(3));
            Assert.That(array.AsSpan().Length, Is.EqualTo(3));
            Assert.That(array.ToArray(), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(array.ToList(), Is.EqualTo(new[] { 1, 2, 3 }), "it enumerates");
            Assert.That(EquatableArray<int>.Empty, Is.Empty);
            Assert.That(EquatableArray.Create<int>([1, 2, 3]), Is.EqualTo(array));
        }
    }

    [Test]
    public void AddRange_Always_ReturnsNewArrayLeavingOriginal()
    {
        EquatableArray<int> a = [1];
        var b = a.AddRange([2, 3]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Has.Length.EqualTo(1));
            Assert.That(b, Is.EqualTo((EquatableArray<int>)[1, 2, 3]));
            Assert.That(a.AddRange([]), Is.EqualTo(a));
        }
    }

    [Test]
    public void Equals_RecordsHoldingEqualArrays_IsTrue()
    {
        var a = new TypeDeclaration(Name: "T", Members: [new FieldDeclaration(Type: Types.Int32, Name: "x")]);
        var b = new TypeDeclaration(Name: "T", Members: [new FieldDeclaration(Type: Types.Int32, Name: "x")]);
        var c = b.AddMembers(new FieldDeclaration(Type: Types.Int32, Name: "y"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(c));
        }
    }

    [Test]
    public void ToEquatableArray_LazySequenceYieldingNothing_IsEmptyLikeDefault()
    {
        var none = new[] { 1 }.Where(x => x > 5).ToEquatableArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none.IsEmpty, Is.True);
            Assert.That(none, Is.EqualTo(default(EquatableArray<int>)), "a generator's cache compares these");
            Assert.That(none.GetHashCode(), Is.EqualTo(default(EquatableArray<int>).GetHashCode()));
        }
    }
}

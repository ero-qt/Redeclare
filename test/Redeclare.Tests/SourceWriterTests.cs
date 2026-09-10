using NUnit.Framework;

namespace Redeclare.Tests;

[TestFixture]
public sealed class SourceWriterTests
{
    [Test]
    public void Block_NestedWrites_IndentsAndKeepsBlankLinesClean()
    {
        SourceWriter writer = new(RenderOptions.Default with { Indent = "  " });
        writer.WriteLine("class C");
        using (writer.Block())
        {
            writer.WriteLine("int x;");
            writer.BlankLine();
            writer.BlankLine();
            writer.WriteLine("int y;\nint z;");
        }

        Assert.That(writer.ToString(), Is.EqualTo("class C\n{\n  int x;\n\n  int y;\n  int z;\n}\n"));
    }

    [Test]
    public void WriteLine_ConfiguredNewLine_UsesIt()
    {
        SourceWriter writer = new(RenderOptions.Default with { NewLine = "\r\n" });
        writer.WriteLine("a");
        writer.WriteLine("b");

        Assert.That(writer.ToString(), Is.EqualTo("a\r\nb\r\n"));
    }

    [Test]
    public void Unindent_AtDepthZero_Throws()
    {
        SourceWriter writer = new();

        Assert.That(writer.Unindent, Throws.InvalidOperationException);
    }

    [Test]
    public void WriteLines_InsideABlock_WritesEachAtTheDepthInForce()
    {
        SourceWriter writer = new();
        writer.WriteLine("class C");
        using (writer.Block())
        {
            writer.WriteLines(["int x;", "int y;"]);
            Assert.That(writer.Depth, Is.EqualTo(1));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(writer.Depth, Is.Zero);
            Assert.That(writer.ToString(), Is.EqualTo("class C\n{\n    int x;\n    int y;\n}\n"));
        }
    }

    [Test]
    public void BeginLine_InsideABlock_IndentsOnceAndHandsOutTheBuffer()
    {
        SourceWriter writer = new();
        using (writer.Block())
        {
            writer.BeginLine().Append("int ").Append('x');
            writer.BeginLine().Append(';');
            writer.EndLine();
        }

        Assert.That(writer.ToString(), Is.EqualTo("{\n    int x;\n}\n"));
    }
}

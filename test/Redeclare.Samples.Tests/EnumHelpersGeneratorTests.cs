using EnumHelpers;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using System;
using System.Linq;

namespace Redeclare.Samples.Tests;

[TestFixture]
public sealed class EnumHelpersGeneratorTests
{
    private const string Source = """
        namespace Shapes.Colors
        {
            [EnumHelpers.EnumHelpers]
            public enum Color { Red, Green, Blue = 2, Azure = Blue }

            public static class Outer
            {
                [EnumHelpers.EnumHelpers(ClassName = "SizeHelpers")]
                internal enum Size : byte { Small = 1, Large = 2 }
            }
        }
        """;

    private static string Color => Generated("class ColorExtensions");

    private static string Size => Generated("class SizeHelpers");

    private static string Generated(string marker)
    {
        return GeneratorRunner.Run<EnumHelpersGenerator>(Source).Single(f => f.Contains(marker, StringComparison.Ordinal));
    }

    [Test]
    public void Run_MarkedEnums_GenerateOneFileEach()
    {
        var files = GeneratorRunner.Run<EnumHelpersGenerator>(Source);

        Assert.That(files, Has.Count.EqualTo(3), "the attribute and one file per enum");
    }

    [Test]
    public void Run_PublicEnum_GeneratesPublicHelpersQualifyingTheEnum()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Color, Does.Contain("public static class ColorExtensions"));
            Assert.That(Color, Does.Contain("public static string ToStringFast(this global::Shapes.Colors.Color value)"));
            Assert.That(Color, Does.Contain("global::Shapes.Colors.Color.Blue => nameof(global::Shapes.Colors.Color.Blue),"));
            Assert.That(Color, Does.Contain("case nameof(global::Shapes.Colors.Color.Red):"));
        }
    }

    [Test]
    public void Run_AliasedMember_IsNotASecondArm()
    {
        Assert.That(Color, Does.Not.Contain("Azure =>"));
    }

    [Test]
    public void Run_NestedInternalEnum_GeneratesInternalHelpersAtTheNamespaceLevel()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Size, Does.Contain("internal static class SizeHelpers"));
            Assert.That(Size, Does.Contain("this global::Shapes.Colors.Outer.Size value"), "a nested enum qualifies through its container");
        }
    }

    [Test]
    public void Run_RepositoryDefaults_WriteBlockBodies()
    {
        Assert.That(Color, Does.Contain("return value switch"));
    }

    [Test]
    public void Run_ConsumerEditorconfig_DecidesBodiesAndIndentation()
    {
        var files = GeneratorRunner.Run<EnumHelpersGenerator>(Source, new GeneratorRunner.Editorconfig
        {
            ["csharp_style_expression_bodied_methods"] = "true:suggestion",
            ["indent_style"] = "space",
            ["indent_size"] = "2",
        });
        var color = files.Single(f => f.Contains("class ColorExtensions", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(color, Does.Contain("public static string ToStringFast(this global::Shapes.Colors.Color value) =>\n    value switch"));
            Assert.That(color, Does.Contain("\n  public static bool IsDefined"), "two-space indentation");
        }
    }

    [Test]
    public void ReasonsAfterAnEdit_UnrelatedFile_AreCached()
    {
        var reasons = GeneratorRunner.ReasonsAfterAnEdit<EnumHelpersGenerator>(Source, EnumHelpersGenerator.EnumsStep);

        Assert.That(reasons, Has.All.EqualTo(IncrementalStepRunReason.Cached));
    }

    [Test]
    public void ReasonsAfterAnEdit_RenamedMember_AreModified()
    {
        var reasons = GeneratorRunner.ReasonsAfterAnEdit<EnumHelpersGenerator>(
            Source,
            EnumHelpersGenerator.EnumsStep,
            Source.Replace("Green", "Emerald", StringComparison.Ordinal));

        Assert.That(reasons, Does.Contain(IncrementalStepRunReason.Modified));
    }
}

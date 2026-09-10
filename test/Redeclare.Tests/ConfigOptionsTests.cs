using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Redeclare.Tests;

[TestFixture]
public sealed class ConfigOptionsTests
{
    [Test]
    public void ToCSharpVersion_SpecifiedVersion_MapsToTheEffectiveOne()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(LanguageVersion.CSharp9.ToCSharpVersion(), Is.EqualTo(CSharpVersion.CSharp9));
            Assert.That(LanguageVersion.Latest.ToCSharpVersion(), Is.Not.EqualTo(CSharpVersion.Latest), "Latest resolves to a number");
            Assert.That((int)LanguageVersion.Default.ToCSharpVersion(), Is.GreaterThanOrEqualTo((int)CSharpVersion.CSharp12));
        }
    }

    [Test]
    public void From_EmptyConfig_IsDefault()
    {
        Assert.That(RenderOptions.From(new FakeConfig()), Is.EqualTo(RenderOptions.Default));
    }

    [Test]
    public void From_IndentAndNewLine_ReadAsEditorconfigWritesThem()
    {
        var tabs = RenderOptions.From(new FakeConfig { ["indent_style"] = "tab", ["end_of_line"] = "crlf" });
        var two = RenderOptions.From(new FakeConfig { ["indent_style"] = "space", ["indent_size"] = "2" });
        var sizeOnly = RenderOptions.From(new FakeConfig { ["indent_size"] = "3" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tabs.Indent, Is.EqualTo("\t"));
            Assert.That(tabs.NewLine, Is.EqualTo("\r\n"));
            Assert.That(two.Indent, Is.EqualTo("  "));
            Assert.That(sizeOnly.Indent, Is.EqualTo("   "));
        }
    }

    [Test]
    public void From_StyleRules_IgnoreTheSeverity()
    {
        var options = RenderOptions.From(new FakeConfig
        {
            ["csharp_style_namespace_declarations"] = "block_scoped:warning",
            ["dotnet_style_predefined_type_for_locals_parameters_members"] = "false:suggestion",
            ["csharp_style_expression_bodied_methods"] = "when_on_single_line:silent",
            ["csharp_style_expression_bodied_properties"] = "true",
            ["csharp_style_expression_bodied_accessors"] = "false",
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.NamespaceDeclarations, Is.EqualTo(NamespaceDeclarationPreference.BlockScoped));
            Assert.That(options.PredefinedTypeKeywords, Is.False);
            Assert.That(options.Methods, Is.EqualTo(ExpressionBodyPreference.WhenOnSingleLine));
            Assert.That(options.Properties, Is.EqualTo(ExpressionBodyPreference.WhenPossible));
            Assert.That(options.Accessors, Is.EqualTo(ExpressionBodyPreference.Never));
            Assert.That(options.Constructors, Is.EqualTo(RenderOptions.Default.Constructors), "unset keys keep the default");
        }
    }

    [Test]
    public void MsBuildProperty_Forwarded_ReadsWithThePrefix()
    {
        var config = new FakeConfig
        {
            ["build_property.RootNamespace"] = "Acme",
            ["build_property.Empty"] = "",
            ["build_metadata.AdditionalFiles.Kind"] = "schema",
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(config.MsBuildProperty("RootNamespace"), Is.EqualTo("Acme"));
            Assert.That(config.MsBuildProperty("Empty"), Is.Null, "an empty property reads as absent");
            Assert.That(config.MsBuildProperty("Missing"), Is.Null);
            Assert.That(config.MsBuildMetadata("AdditionalFiles", "Kind"), Is.EqualTo("schema"));
        }
    }

    [Test]
    public void MsBuildTyped_Forwarded_ConvertsAsMsBuildDoes()
    {
        var config = new FakeConfig
        {
            ["build_property.Enabled"] = "True",
            ["build_property.Count"] = "12",
            ["build_property.Level"] = "high",
            ["build_property.Names"] = "a; b;;c ",
            ["build_property.Bad"] = "maybe",
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(config.MsBuildBoolean("Enabled"), Is.True);
            Assert.That(config.MsBuildBoolean("Bad"), Is.Null);
            Assert.That(config.MsBuildInt32("Count"), Is.EqualTo(12));
            Assert.That(config.MsBuildInt32("Bad"), Is.Null);
            Assert.That(config.MsBuildEnum<Level>("Level"), Is.EqualTo(Level.High));
            Assert.That(config.MsBuildEnum<Level>("Bad"), Is.Null);
            Assert.That(config.MsBuildList("Names"), Is.EqualTo((EquatableArray<string>)["a", "b", "c"]));
            Assert.That(config.MsBuildList("Missing").IsEmpty, Is.True);
        }
    }

    private enum Level
    {
        Low,
        High,
    }

    private sealed class FakeConfig : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values = new(KeyComparer);

        public string this[string key]
        {
            set => _values[key] = value;
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            return _values.TryGetValue(key, out value);
        }
    }
}

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Redeclare.Extensions.Tests;

[TestFixture]
public sealed class RenderOptionsFromTests
{
    [Test]
    public void From_ParseOptions_TakesTheLanguageVersionAndNothingElse()
    {
        var options = RenderOptions.From(new CSharpParseOptions(LanguageVersion.CSharp9));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Version, Is.EqualTo(CSharpVersion.CSharp9));
            Assert.That(options, Is.EqualTo(RenderOptions.Default with { Version = CSharpVersion.CSharp9 }));
        }
    }

    [Test]
    public void From_ParseOptionsAtLatest_ResolvesToANumber()
    {
        var options = RenderOptions.From(new CSharpParseOptions(LanguageVersion.Latest));

        Assert.That(options.Version, Is.Not.EqualTo(CSharpVersion.Latest));
    }

    [Test]
    public void From_ConfigAndParseOptions_ReadsBoth()
    {
        var config = new FakeConfig
        {
            ["indent_style"] = "tab",
            ["csharp_style_expression_bodied_methods"] = "true:silent",
        };

        var options = RenderOptions.From(config, new CSharpParseOptions(LanguageVersion.CSharp11));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.Indent, Is.EqualTo("\t"), "from the editorconfig");
            Assert.That(options.Methods, Is.EqualTo(ExpressionBodyPreference.WhenPossible), "from the editorconfig");
            Assert.That(options.Version, Is.EqualTo(CSharpVersion.CSharp11), "from the parse options");
        }
    }

    [Test]
    public void From_EmptyConfigAndParseOptions_IsTheDefaultsAtThatVersion()
    {
        var options = RenderOptions.From(new FakeConfig(), new CSharpParseOptions(LanguageVersion.CSharp12));

        Assert.That(options, Is.EqualTo(RenderOptions.Default with { Version = CSharpVersion.CSharp12 }));
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

using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Redeclare.Samples.Tests;

/// <summary>
///     Runs a sample generator over source text the way the compiler would, and checks the result compiles.
/// </summary>
internal static class GeneratorRunner
{
    /// <summary>
    ///     The generated files' text, after asserting the whole compilation has no errors.
    /// </summary>
    public static IReadOnlyList<string> Run<TGenerator>(string source, AnalyzerConfigOptions? editorconfig = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        var (generated, errors) = TryRun<TGenerator>(source, editorconfig);

        Assert.That(
            errors,
            Is.Empty,
            () => $"Generated output did not compile:\n{string.Join("\n\n", generated)}\n\nErrors:\n{string.Join("\n", errors)}");

        return generated;
    }

    /// <summary>
    ///     The generated files' text and whatever errors the compilation ends up with.
    /// </summary>
    public static (IReadOnlyList<string> Generated, IReadOnlyList<Diagnostic> Errors) TryRun<TGenerator>(
        string source,
        AnalyzerConfigOptions? editorconfig = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        var compilation = Compile(CSharpSyntaxTree.ParseText(source));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TGenerator().AsSourceGenerator()],
            optionsProvider: editorconfig is null ? null : new FixedOptionsProvider(editorconfig));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        var errors = output.GetDiagnostics().Concat(generatorDiagnostics).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var generated = output.SyntaxTrees.Skip(1).Select(t => t.ToString()).ToList();

        return (generated, errors);
    }

    /// <summary>
    ///     Runs the generator, edits a file, runs it again, and returns why each output of the named step ran the
    ///     second time. <c>Cached</c> means the pipeline compared the values and stopped there. The edit lands in a
    ///     file the generator does not read, unless <paramref name="replacement"/> replaces the marked one.
    /// </summary>
    public static IReadOnlyList<IncrementalStepRunReason> ReasonsAfterAnEdit<TGenerator>(
        string source,
        string trackingName,
        string? replacement = null)
        where TGenerator : IIncrementalGenerator, new()
    {
        const string unrelatedName = "Unrelated.cs";
        var marked = CSharpSyntaxTree.ParseText(source, path: "Marked.cs");
        var unrelated = CSharpSyntaxTree.ParseText("class Unrelated { }", path: unrelatedName);
        var compilation = Compile(marked, unrelated);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new TGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);

        var edited = replacement is null
            ? compilation.ReplaceSyntaxTree(unrelated, CSharpSyntaxTree.ParseText("class Unrelated { int x; }", path: unrelatedName))
            : compilation.ReplaceSyntaxTree(marked, CSharpSyntaxTree.ParseText(replacement, path: "Marked.cs"));

        driver = driver.RunGenerators(edited);

        var steps = driver.GetRunResult().Results[0].TrackedSteps;
        Assert.That(
            steps.ContainsKey(trackingName),
            Is.True,
            () => $"No step named '{trackingName}'; the pipeline has {string.Join(", ", steps.Keys)}.");

        return steps[trackingName].SelectMany(step => step.Outputs).Select(output => output.Reason).ToList();
    }

    private static CSharpCompilation Compile(params SyntaxTree[] trees)
    {
        return CSharpCompilation.Create(
            "Sample.Fixture",
            trees,
            Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    ///     Editorconfig values as a dictionary, for the tests that drive a generator's style.
    /// </summary>
    public sealed class Editorconfig : AnalyzerConfigOptions
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

    /// <summary>
    ///     The same options for every file, standing in for an editorconfig.
    /// </summary>
    private sealed class FixedOptionsProvider(AnalyzerConfigOptions options) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return options;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return options;
        }
    }
}

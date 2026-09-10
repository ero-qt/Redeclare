using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System.Linq;

namespace Redeclare.Tests;

/// <summary>
///     Compiles C# text against the .NET 10 reference assemblies, so rendered code is proven to be C#, and finds
///     the symbols a test reads from the result.
/// </summary>
internal static class Compiling
{
    public static CSharpCompilation Compile(string source, LanguageVersion version = LanguageVersion.Latest, params string[] more)
    {
        CSharpParseOptions parse = new(version);
        var trees = new[] { source }.Concat(more).Select(text => CSharpSyntaxTree.ParseText(text, parse));
        bool nullable = version.MapSpecifiedToEffectiveVersion() >= LanguageVersion.CSharp8;

        return CSharpCompilation.Create(
            "Redeclare.Tests.Fixture",
            trees,
            Net100.References.All,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: nullable ? NullableContextOptions.Enable : NullableContextOptions.Disable,
                allowUnsafe: true));
    }

    /// <summary>
    ///     Asserts that the source has no compile errors, quoting the source and the errors when it does.
    /// </summary>
    public static CSharpCompilation AssertCompiles(string source, LanguageVersion version = LanguageVersion.Latest, params string[] more)
    {
        var compilation = Compile(source, version, more);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.That(errors, Is.Empty, () => $"Rendered source did not compile:\n{source}\n\nErrors:\n{string.Join("\n", errors)}");

        return compilation;
    }

    /// <summary>
    ///     The type with this full metadata name, which the test knows exists.
    /// </summary>
    public static INamedTypeSymbol Type(this Compilation compilation, string fullMetadataName)
    {
        var type = compilation.GetTypeByMetadataName(fullMetadataName);

        Assert.That(type, Is.Not.Null, () => $"No type '{fullMetadataName}' in the compilation.");

        return type!;
    }

    /// <summary>
    ///     The type read as a declaration and put back in its namespace, the way a generator writes one file per
    ///     type.
    /// </summary>
    public static CompilationUnit ToFile(this INamedTypeSymbol type, ReadOptions? options = null)
    {
        return new CompilationUnit(Members: [type.ContainingNamespace.ToDeclaration() with { Members = [type.ToDeclaration(options)] }]);
    }
}

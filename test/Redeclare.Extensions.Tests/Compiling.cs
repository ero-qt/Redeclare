using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Redeclare.Extensions.Tests;

/// <summary>
///     Compiles C# text against the .NET 10 reference assemblies, so a predefined type can be compared with the same
///     type read from a symbol.
/// </summary>
internal static class Compiling
{
    public static CSharpCompilation Compile(string source)
    {
        return CSharpCompilation.Create(
            "Redeclare.Extensions.Tests.Fixture",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    public static INamedTypeSymbol Type(this Compilation compilation, string fullMetadataName)
    {
        var type = compilation.GetTypeByMetadataName(fullMetadataName);

        Assert.That(type, Is.Not.Null, () => $"No type '{fullMetadataName}' in the compilation.");

        return type!;
    }
}

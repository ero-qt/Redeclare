using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Redeclare;

namespace Redeclare.Consumer;

/// <summary>
///     Names a type from every folder of both packages, so a file missing from contentFiles fails the build and the
///     partial record the extensions package adds to is proven to join up.
/// </summary>
internal static class Uses
{
    public static string Render()
    {
        var type = new TypeDeclaration(
            Name: "C",
            Members: [
                new MethodDeclaration(
                    Accessibility: Accessibility.Public,
                    ReturnType: new NamedTypeReference(Name: "String", ContainingNamespace: "System", SpecialType: SpecialType.System_String),
                    Name: "Get",
                    Body: Snippet.Expression("null")),
            ]);

        var unit = new CompilationUnit(Members: [new NamespaceDeclaration(Name: "N", Members: [type])]);

        // From(ParseOptions) comes from Redeclare.Extensions, which adds to the core's RenderOptions record.
        var options = RenderOptions.From(new CSharpParseOptions(LanguageVersion.CSharp12)) with { Qualification = Qualification.Minimal };

        return unit.Render(options);
    }
}

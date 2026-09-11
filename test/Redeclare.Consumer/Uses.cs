using Microsoft.CodeAnalysis;
using Redeclare;

namespace Redeclare.Consumer;

/// <summary>
///     Names a type from every folder of the package, so a file missing from contentFiles fails the build.
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

        return unit.Render(RenderOptions.Default with { Qualification = Qualification.Minimal });
    }
}

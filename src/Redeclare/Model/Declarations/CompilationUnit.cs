using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Redeclare;

/// <summary>
///     Represents one source file: whatever stands above the usings, then the usings, then its members, which
///     are namespaces and types. A file may hold several namespaces, nested ones, and types in the global
///     namespace side by side.
/// </summary>
/// <remarks>
///     <para>
///         "Compilation unit" is the C# specification's word for one source file as the compiler sees it. A
///         file declares no symbol of its own, which is why this record has no <c>Declaration</c> suffix.
///     </para>
///     <para>
///         <paramref name="Header"/> is one text written as it is: the header comment, <c>#nullable enable</c>,
///         <c>#pragma warning disable</c>, whatever belongs above the usings. It is empty unless written, so a
///         generated file says <c>&lt;auto-generated/&gt;</c> itself.
///     </para>
/// </remarks>
/// <param name="Header">Text written as it is above the usings, or <see langword="null"/> for none.</param>
/// <param name="Usings">The using directives without the keyword: <c>System</c>, <c>static System.Math</c>, <c>Alias = Ns.Type</c>.</param>
/// <param name="Members">
///     The namespaces and types, in order. A type here is in the global namespace. A lone namespace renders
///     file-scoped when the options ask for it. Anything else renders as blocks.
/// </param>
/// <param name="Overrides">Render options for the whole file, applied over the options the file is rendered with.</param>
internal sealed record CompilationUnit(
    Snippet? Header = null,
    EquatableArray<string> Usings = default,
    EquatableArray<MemberDeclaration> Members = default,
    RenderOverrides? Overrides = null)
{
    /// <summary>
    ///     Gets a file name for <c>AddSource</c>: the namespaces, the types the file's type is declared in, then
    ///     its own name, arity suffixes included, then <c>.g.cs</c>.
    /// </summary>
    public string HintName
    {
        get
        {
            if (Members.IsEmpty)
            {
                return "Generated.g.cs";
            }

            StringBuilder name = new();
            var member = Members[0];
            while (member is NamespaceDeclaration { Members: { Length: 1 } inner } ns)
            {
                if (ns.Name.Length > 0)
                {
                    name.Append(ns.Name).Append('.');
                }

                member = inner[0];
            }

            if (member is not TypeDeclaration type)
            {
                return "Generated.g.cs";
            }

            List<TypeDeclaration> chain = [];
            for (var current = type; current is not null; current = current.ContainingType)
            {
                chain.Add(current);
            }

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                name.Append(chain[i].Name);
                if (!chain[i].TypeParameters.IsEmpty)
                {
                    name.Append('`').Append(chain[i].TypeParameters.Length.ToString(CultureInfo.InvariantCulture));
                }

                name.Append('.');
            }

            return name.Append("g.cs").ToString();
        }
    }

    /// <summary>
    ///     Renders the file to text.
    /// </summary>
    public string Render(RenderOptions? options = null)
    {
        return CSharpRenderer.Render(this, options ?? RenderOptions.Default);
    }
}

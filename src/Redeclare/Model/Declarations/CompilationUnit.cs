using Microsoft.CodeAnalysis;
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
///         <paramref name="Header"/> is one text written as it is at the top of the file. It is empty unless
///         written, so a generated file says <c>&lt;auto-generated/&gt;</c> itself. The nullable context and the
///         warnings to disable are written after it, as directives, so they follow the render options.
///     </para>
/// </remarks>
/// <param name="Header">Text written as it is at the top of the file, or <see langword="null"/> for none.</param>
/// <param name="NullableContext">
///     The <c>#nullable</c> directive under the header, or <see langword="null"/> for none. Written only under C# 8
///     or later, since the directive does not exist before that.
/// </param>
/// <param name="DisabledWarnings">
///     The warnings a <c>#pragma warning disable</c> under the header names: <c>CS1591</c>, <c>IDE0005</c>. Empty
///     for no directive.
/// </param>
/// <param name="Usings">The using directives without the keyword: <c>System</c>, <c>static System.Math</c>, <c>Alias = Ns.Type</c>.</param>
/// <param name="Members">
///     The namespaces and types, in order. A type here is in the global namespace. A lone namespace renders
///     file-scoped when the options ask for it. Anything else renders as blocks.
/// </param>
internal sealed record CompilationUnit(
    Snippet? Header = null,
    NullableContextOptions? NullableContext = null,
    EquatableArray<string> DisabledWarnings = default,
    EquatableArray<string> Usings = default,
    EquatableArray<MemberDeclaration> Members = default)
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

            if (GetNamed(member) is not var (first, containing))
            {
                return "Generated.g.cs";
            }

            List<(string Name, int Arity)> chain = [first];
            for (var current = containing; current is not null; current = current.ContainingType)
            {
                chain.Add((current.Name, current.TypeParameters.Length));
            }

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                name.Append(chain[i].Name);
                if (chain[i].Arity > 0)
                {
                    name.Append('`').Append(chain[i].Arity.ToString(CultureInfo.InvariantCulture));
                }

                name.Append('.');
            }

            return name.Append("g.cs").ToString();
        }
    }

    /// <summary>
    ///     Finds the name a member gives a file, with the arity that tells <c>List&lt;T&gt;</c> from <c>List</c>, and the
    ///     type it is nested in. An extension block is named after its receiver type.
    /// </summary>
    private static ((string Name, int Arity) Own, TypeDeclaration? Containing)? GetNamed(MemberDeclaration member)
    {
        return member switch
        {
            TypeDeclaration type => ((type.Name, type.TypeParameters.Length), type.ContainingType),
            DelegateDeclaration @delegate => ((@delegate.Name, @delegate.TypeParameters.Length), @delegate.ContainingType),
            ExtensionDeclaration { ContainingType: { } outer, Receiver.Type: NamedTypeReference receiver } extension
                => ((receiver.Name, extension.TypeParameters.Length), outer),
            _ => null,
        };
    }

    /// <summary>
    ///     Renders the file to text.
    /// </summary>
    public string Render(RenderOptions? options = null)
    {
        return CSharpRenderer.Render(this, options ?? RenderOptions.Default);
    }
}

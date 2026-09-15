using System;
using System.Collections.Generic;

namespace Redeclare;

internal static partial class CSharpRenderer
{
    /// <summary>
    ///     Collects the namespaces the file would need as usings under <see cref="Qualification.Minimal"/>, sorted.
    ///     Every type reference counts, holes in snippets included. Types the options write as a keyword are left out.
    ///     The file's own namespace is left in.
    /// </summary>
    public static SortedSet<string> CollectNamespaces(CompilationUnit unit, RenderOptions options)
    {
        SortedSet<string> namespaces = new(StringComparer.Ordinal);
        foreach (var reference in unit.GetTypeReferences())
        {
            foreach (var type in reference.DescendantsAndSelf())
            {
                if (type is NamedTypeReference { ContainingType: null, ContainingNamespace: { Length: > 0 } ns } named && !WritesKeyword(named, options))
                {
                    namespaces.Add(ns);
                }
            }
        }

        return namespaces;
    }
}

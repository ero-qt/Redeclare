using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace Redeclare;

/// <summary>
///     Provides the checks a generator or analyzer makes on symbols before it acts: is this type partial all the
///     way out, is this type the one I mean, is this attribute allowed here. Types are always compared as
///     symbols: resolve the one you mean once with <c>Compilation.GetTypeByMetadataName</c> and pass it in.
///     Every answer is a plain value. Reporting anything about it is the caller's business.
/// </summary>
internal static class Checks
{
    /// <summary>
    ///     Gets a value indicating whether every declaration of the type is marked <c>partial</c>, which is what
    ///     adding generated members to it requires. A type with no source declarations is not partial.
    /// </summary>
    public static bool IsPartial(this INamedTypeSymbol type)
    {
        bool any = false;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not BaseTypeDeclarationSyntax declaration)
            {
                return false;
            }

            any = true;
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }
        }

        return any;
    }

    /// <summary>
    ///     Gets a value indicating whether the type and every type containing it are partial, so a nested partial
    ///     can be generated.
    /// </summary>
    public static bool IsPartialThroughout(this INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (!current.IsPartial())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets a value indicating whether the method is declared <c>partial</c>, as either half.
    /// </summary>
    public static bool IsPartial(this IMethodSymbol method)
    {
        return method.IsPartialDefinition || method.PartialDefinitionPart is not null || method.PartialImplementationPart is not null;
    }

    /// <summary>
    ///     Gets a value indicating whether the two types have the same original definition: <c>List&lt;int&gt;</c>
    ///     is <c>List&lt;T&gt;</c>. Nullable annotations do not take part.
    /// </summary>
    public static bool Is(this ITypeSymbol type, ITypeSymbol other)
    {
        return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, other.OriginalDefinition);
    }

    /// <summary>
    ///     Gets a value indicating whether a base class of the type, at any depth, is <paramref name="baseType"/>.
    /// </summary>
    public static bool InheritsFrom(this ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Is(baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets a value indicating whether the type implements <paramref name="interfaceType"/>, directly or
    ///     through a base.
    /// </summary>
    public static bool Implements(this ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        foreach (var @interface in type.AllInterfaces)
        {
            if (@interface.Is(interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     The full metadata name of a named type: namespace, then the type and its containing types with arity
    ///     suffixes, nested ones after <c>+</c>. What <c>GetTypeByMetadataName</c> takes. For messages and lookups,
    ///     not for comparison.
    /// </summary>
    public static string FullMetadataName(this INamedTypeSymbol type)
    {
        List<string> parts = [];
        var outermost = type;
        for (var current = type; current is not null; current = current.ContainingType)
        {
            parts.Insert(0, current.MetadataName);
            outermost = current;
        }

        string name = string.Join("+", parts);

        return outermost.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString() + "." + name
            : name;
    }

    /// <summary>
    ///     The targets an attribute class allows, from its <c>[AttributeUsage]</c>, inherited ones included.
    ///     <c>AttributeTargets.All</c> when it declares none.
    /// </summary>
    public static AttributeTargets ValidTargets(this INamedTypeSymbol attributeClass, Compilation compilation)
    {
        var usage = Usage(attributeClass, compilation);

        return usage is { ConstructorArguments.Length: 1 } && usage.ConstructorArguments[0].Value is int targets
            ? (AttributeTargets)targets
            : AttributeTargets.All;
    }

    /// <summary>
    ///     Gets a value indicating whether the attribute class allows more than one instance on a target.
    /// </summary>
    public static bool AllowsMultiple(this INamedTypeSymbol attributeClass, Compilation compilation)
    {
        if (Usage(attributeClass, compilation) is not { } usage)
        {
            return false;
        }

        foreach (var named in usage.NamedArguments)
        {
            if (named.Key == "AllowMultiple" && named.Value.Value is bool allow)
            {
                return allow;
            }
        }

        return false;
    }

    /// <summary>
    ///     The <c>AttributeTargets</c> flag a symbol counts as, or <see langword="null"/> for a symbol no attribute
    ///     targets.
    /// </summary>
    public static AttributeTargets? AttributeTarget(this ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol { TypeKind: TypeKind.Class } => AttributeTargets.Class,
            INamedTypeSymbol { TypeKind: TypeKind.Struct } => AttributeTargets.Struct,
            INamedTypeSymbol { TypeKind: TypeKind.Interface } => AttributeTargets.Interface,
            INamedTypeSymbol { TypeKind: TypeKind.Enum } => AttributeTargets.Enum,
            INamedTypeSymbol { TypeKind: TypeKind.Delegate } => AttributeTargets.Delegate,
            IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => AttributeTargets.Constructor,
            IMethodSymbol => AttributeTargets.Method,
            IPropertySymbol => AttributeTargets.Property,
            IFieldSymbol => AttributeTargets.Field,
            IEventSymbol => AttributeTargets.Event,
            IParameterSymbol => AttributeTargets.Parameter,
            ITypeParameterSymbol => AttributeTargets.GenericParameter,
            IAssemblySymbol => AttributeTargets.Assembly,
            IModuleSymbol => AttributeTargets.Module,
            _ => null,
        };
    }

    /// <summary>
    ///     Gets a value indicating whether the attribute's class allows placement on <paramref name="target"/>.
    /// </summary>
    public static bool IsValidOn(this AttributeData attribute, ISymbol target, Compilation compilation)
    {
        if (attribute.AttributeClass is not { } attributeClass || target.AttributeTarget() is not { } kind)
        {
            return false;
        }

        return (attributeClass.ValidTargets(compilation) & kind) == kind;
    }

    /// <summary>
    ///     The <c>[AttributeUsage]</c> on the class or the nearest base that has one.
    /// </summary>
    private static AttributeData? Usage(INamedTypeSymbol attributeClass, Compilation compilation)
    {
        var usageClass = compilation.GetTypeByMetadataName("System.AttributeUsageAttribute");
        if (usageClass is null)
        {
            return null;
        }

        for (var current = attributeClass; current is not null; current = current.BaseType)
        {
            if (current.GetAttribute(usageClass) is { } usage)
            {
                return usage;
            }
        }

        return null;
    }
}

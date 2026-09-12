using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace Redeclare;

/// <summary>
///     Provides methods that read Roslyn symbols into declarations and type references. This is the only code
///     in the library that touches <c>ISymbol</c>, and everything it returns is a value the pipeline can cache on.
///     The <c>ToDeclaration()</c> and <c>ToTypeReference()</c> extensions call into here.
/// </summary>
/// <remarks>
///     <para>
///         Reading gives the shape a symbol knows, not the text it came from: a method comes back with no body,
///         a property with auto accessors, an interface member without a redundant <c>public</c>. Only the
///         definition half of a partial member carries <c>partial</c>, and no type does.
///     </para>
///     <para>
///         Constants keep their meaning: a default of <c>Level.High</c> comes back as a snippet with a hole for
///         <c>Level</c> and the member name that has the value, and an attribute's <c>typeof(List&lt;int&gt;)</c>
///         comes back with a hole for the type, so both qualify at render time.
///     </para>
/// </remarks>
internal static partial class SymbolReader
{
    /// <summary>
    ///     Reads a class, struct, interface, enum, record or delegate.
    /// </summary>
    public static TypeDeclaration ReadType(INamedTypeSymbol type, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        if (type.IsExtension)
        {
            throw new ArgumentException($"'{type.Name}' is an extension block. Use ToExtensionDeclaration.", nameof(type));
        }

        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate))
        {
            throw new NotSupportedException(
                $"'{type.Name}' is a {type.TypeKind}. Only classes, structs, interfaces, enums, records and delegates have a typed "
                + "declaration. Use RawMemberDeclaration.");
        }

        var modifiers = Modifiers.None;
        bool classLike = type.TypeKind == TypeKind.Class;
        bool structLike = type.TypeKind == TypeKind.Struct;

        if (type.IsStatic)
        {
            modifiers |= Modifiers.Static;
        }

        if (type.IsAbstract && classLike)
        {
            modifiers |= Modifiers.Abstract;
        }

        if (type.IsSealed && classLike)
        {
            modifiers |= Modifiers.Sealed;
        }

        if (type.IsReadOnly && structLike)
        {
            modifiers |= Modifiers.ReadOnly;
        }

        if (type.IsRefLikeType)
        {
            modifiers |= Modifiers.Ref;
        }

        if (type.IsFileLocal)
        {
            modifiers |= Modifiers.File;
        }

        var baseType = classLike && type.BaseType is { SpecialType: not SpecialType.System_Object } declaredBase
            ? ReadTypeReference(declaredBase)
            : null;

        List<TypeReference> interfaces = [];
        if (type.TypeKind != TypeKind.Enum)
        {
            foreach (var @interface in type.Interfaces)
            {
                interfaces.Add(ReadTypeReference(@interface));
            }
        }

        var underlying = type.TypeKind == TypeKind.Enum && type.EnumUnderlyingType is { SpecialType: not SpecialType.System_Int32 } enumType
            ? ReadTypeReference(enumType)
            : null;

        // Reads a delegate's signature from its `Invoke` method, where the symbol API keeps it. The declaration
        // holds it the way the syntax does, as a return type and a parameter list, and has no members.
        var invoke = type.TypeKind == TypeKind.Delegate ? type.DelegateInvokeMethod : null;
        if (invoke is not null && MentionsPointer(invoke))
        {
            modifiers |= Modifiers.Unsafe;
        }

        return new TypeDeclaration(
            DocumentationComment: ReadDocumentation(type, options),
            Attributes: ReadAttributes(type, options),
            // `file` reports as `internal`.
            Accessibility: type.IsFileLocal ? Accessibility.NotApplicable : type.DeclaredAccessibility,
            Modifiers: modifiers,
            TypeKind: type.TypeKind,
            IsRecord: type.IsRecord,
            Name: type.Name,
            ContainingType: type.ContainingType is { } outer ? ReadShape(outer) : null,
            TypeParameters: ReadTypeParameters(type.TypeParameters, options),
            ParameterList: invoke is null ? default : ReadParameters(invoke.Parameters, isExtension: false, options),
            BaseType: invoke is null ? baseType : null,
            Interfaces: invoke is null ? interfaces.ToEquatableArray() : default,
            EnumUnderlyingType: underlying,
            ReturnType: invoke is null ? null : ReadTypeReference(invoke.ReturnType),
            RefKind: invoke?.RefKind ?? RefKind.None,
            Members: options.IncludeMembers && invoke is null ? ReadMembers(type, options) : default);
    }

    /// <summary>
    ///     Reads an extension block: its receiver, its type parameters and its members.
    /// </summary>
    public static ExtensionDeclaration ReadExtension(INamedTypeSymbol extension, ReadOptions? options = null)
    {
        options ??= ReadOptions.Default;

        if (!extension.IsExtension || extension.ExtensionParameter is not { } receiver)
        {
            throw new ArgumentException($"'{extension.Name}' is not an extension block.", nameof(extension));
        }

        return new ExtensionDeclaration(
            Receiver: ReadParameter(receiver, isThis: false, options),
            TypeParameters: ReadTypeParameters(extension.TypeParameters, options),
            Members: options.IncludeMembers ? ReadMembers(extension, options) : default);
    }

    /// <summary>
    ///     Reads a namespace as a declaration with nothing in it, the way a file names the namespace a type lives
    ///     in. The global namespace reads as one with no name, which writes no <c>namespace</c> line.
    /// </summary>
    public static NamespaceDeclaration ReadNamespace(INamespaceSymbol @namespace)
    {
        return new NamespaceDeclaration(Name: @namespace.IsGlobalNamespace ? "" : @namespace.ToDisplayString());
    }

    /// <summary>
    ///     The shape of a containing type: kind, name and the names of its type parameters, which is all a part of
    ///     it has to repeat.
    /// </summary>
    private static TypeDeclaration ReadShape(INamedTypeSymbol type)
    {
        var typeParameters = new TypeParameterDeclaration[type.TypeParameters.Length];
        for (int i = 0; i < typeParameters.Length; i++)
        {
            typeParameters[i] = new TypeParameterDeclaration(Name: type.TypeParameters[i].Name, Variance: type.TypeParameters[i].Variance);
        }

        return new TypeDeclaration(
            Name: type.Name,
            TypeKind: type.TypeKind,
            IsRecord: type.IsRecord,
            Modifiers: Modifiers.Partial,
            TypeParameters: typeParameters,
            ContainingType: type.ContainingType is { } outer ? ReadShape(outer) : null);
    }

    private static EquatableArray<MemberDeclaration> ReadMembers(INamedTypeSymbol type, ReadOptions options)
    {
        List<MemberDeclaration> members = [];
        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared && !options.IncludeImplicitlyDeclared)
            {
                continue;
            }

            switch (member)
            {
                case INamedTypeSymbol { IsExtension: true } extension:
                {
                    members.Add(ReadExtension(extension, options));
                    break;
                }
                case INamedTypeSymbol
                {
                    TypeKind: TypeKind.Class or TypeKind.Struct or TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate,
                } nested:
                {
                    members.Add(ReadType(nested, options));
                    break;
                }
                case IMethodSymbol { AssociatedSymbol: not null }:
                {
                    break;
                }
                case IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor:
                {
                    members.Add(ReadConstructor(constructor, options));
                    break;
                }
                case IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation } method:
                {
                    members.Add(ReadMethod(method, options));
                    break;
                }
                case IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion } @operator:
                {
                    members.Add(ReadMethod(@operator, options));
                    break;
                }
                case IPropertySymbol property:
                {
                    members.Add(ReadProperty(property, options));
                    break;
                }
                case IFieldSymbol { AssociatedSymbol: not null }:
                {
                    break;
                }
                case IFieldSymbol field when type.TypeKind == TypeKind.Enum:
                {
                    members.Add(ReadEnumMember(field, options));
                    break;
                }
                case IFieldSymbol field:
                {
                    members.Add(ReadField(field, options));
                    break;
                }
                case IEventSymbol @event:
                {
                    members.Add(ReadEvent(@event, options));
                    break;
                }
                default:
                {
                    break;
                }
            }
        }

        return members.ToEquatableArray();
    }

    private static EquatableArray<AttributeSpecification> ReadAttributes(ISymbol symbol, ReadOptions options)
    {
        if (!options.IncludeAttributes)
        {
            return default;
        }

        List<AttributeSpecification> attributes = [];
        foreach (var attribute in symbol.GetAttributes())
        {
            if (ReadAttribute(attribute) is { } specification)
            {
                attributes.Add(specification);
            }
        }

        // Reads the `[return: ...]` attributes too. They sit on the return value, not on the method symbol.
        if (symbol is IMethodSymbol method)
        {
            foreach (var attribute in method.GetReturnTypeAttributes())
            {
                if (ReadAttribute(attribute) is { } specification)
                {
                    attributes.Add(specification with { Target = "return" });
                }
            }
        }

        return attributes.ToEquatableArray();
    }

    private static string? ReadDocumentation(ISymbol symbol, ReadOptions options)
    {
        if (!options.IncludeDocumentationComments)
        {
            return null;
        }

        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        // Strips the `<member name="...">` wrapper the compiler adds. It is not part of what was written.
        List<string> kept = [];
        foreach (var line in Snippet.Dedent(xml!))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("<member ", StringComparison.Ordinal) || trimmed == "</member>")
            {
                continue;
            }

            kept.Add(line);
        }

        var body = Snippet.Dedent(string.Join("\n", kept));

        return body.IsEmpty ? null : string.Join("\n", [.. body]);
    }
}

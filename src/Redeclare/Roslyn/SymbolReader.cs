using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace Redeclare;

/// <summary>
///     Reads Roslyn symbols into declarations and type references under one set of <see cref="ReadOptions"/>. This
///     is the only code in the library that touches <c>ISymbol</c>, and everything it returns is a value the
///     pipeline can cache on. The <c>ToDeclaration()</c> and <c>ToTypeReference()</c> extensions call into here.
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
internal sealed partial class SymbolReader
{
    private readonly ReadOptions _options;

    /// <summary>
    ///     Initializes a reader that reads under <paramref name="options"/>.
    /// </summary>
    public SymbolReader(ReadOptions options)
    {
        _options = options;
    }

    /// <summary>
    ///     Gets the reader for <see cref="ReadOptions.Default"/>.
    /// </summary>
    public static SymbolReader Default { get; } = new(ReadOptions.Default);

    /// <summary>
    ///     Gets a reader for <paramref name="options"/>, the default one when they are <see langword="null"/>.
    /// </summary>
    public static SymbolReader Create(ReadOptions? options)
    {
        return options is null ? Default : new SymbolReader(options);
    }

    /// <summary>
    ///     Reads a class, struct, interface, enum, record or delegate.
    /// </summary>
    public TypeDeclaration ReadType(INamedTypeSymbol type)
    {
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

        var invoke = type.TypeKind == TypeKind.Delegate ? type.DelegateInvokeMethod : null;
        if (invoke is not null && MentionsPointer(invoke))
        {
            modifiers |= Modifiers.Unsafe;
        }

        return new TypeDeclaration(
            DocumentationComment: ReadDocumentation(type),
            Attributes: ReadAttributes(type),
            // `file` reports as `internal`.
            Accessibility: type.IsFileLocal ? Accessibility.NotApplicable : type.DeclaredAccessibility,
            Modifiers: modifiers,
            TypeKind: type.TypeKind,
            IsRecord: type.IsRecord,
            Name: type.Name,
            ContainingType: type.ContainingType is { } outer ? ReadShape(outer) : null,
            TypeParameters: ReadTypeParameters(type.TypeParameters),
            ParameterList: invoke is null ? default : ReadParameters(invoke.Parameters, isExtension: false),
            BaseType: invoke is null ? baseType : null,
            Interfaces: invoke is null ? interfaces.ToEquatableArray() : default,
            EnumUnderlyingType: underlying,
            ReturnType: invoke is null ? null : ReadTypeReference(invoke.ReturnType),
            RefKind: invoke?.RefKind ?? RefKind.None,
            Members: _options.IncludeMembers && invoke is null ? ReadMembers(type) : default);
    }

    /// <summary>
    ///     Reads an extension block: its receiver, its type parameters and its members.
    /// </summary>
    public ExtensionDeclaration ReadExtension(INamedTypeSymbol extension)
    {
        if (!extension.IsExtension || extension.ExtensionParameter is not { } receiver)
        {
            throw new ArgumentException($"'{extension.Name}' is not an extension block.", nameof(extension));
        }

        return new ExtensionDeclaration(
            Receiver: ReadParameter(receiver, isThis: false),
            TypeParameters: ReadTypeParameters(extension.TypeParameters),
            Members: _options.IncludeMembers ? ReadMembers(extension) : default);
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

    private EquatableArray<MemberDeclaration> ReadMembers(INamedTypeSymbol type)
    {
        List<MemberDeclaration> members = [];
        foreach (var member in type.GetMembers())
        {
            if (member.IsImplicitlyDeclared && !_options.IncludeImplicitlyDeclared)
            {
                continue;
            }

            switch (member)
            {
                case INamedTypeSymbol { IsExtension: true } extension:
                {
                    members.Add(ReadExtension(extension));
                    break;
                }
                case INamedTypeSymbol
                {
                    TypeKind: TypeKind.Class or TypeKind.Struct or TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate,
                } nested:
                {
                    members.Add(ReadType(nested));
                    break;
                }
                case IMethodSymbol { AssociatedSymbol: not null }:
                {
                    break;
                }
                case IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor:
                {
                    members.Add(ReadConstructor(constructor));
                    break;
                }
                case IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation or MethodKind.Destructor } method:
                {
                    members.Add(ReadMethod(method));
                    break;
                }
                case IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion } @operator:
                {
                    members.Add(ReadMethod(@operator));
                    break;
                }
                case IPropertySymbol property:
                {
                    members.Add(ReadProperty(property));
                    break;
                }
                case IFieldSymbol { AssociatedSymbol: not null }:
                {
                    break;
                }
                case IFieldSymbol field when type.TypeKind == TypeKind.Enum:
                {
                    members.Add(ReadEnumMember(field));
                    break;
                }
                case IFieldSymbol field:
                {
                    members.Add(ReadField(field));
                    break;
                }
                case IEventSymbol @event:
                {
                    members.Add(ReadEvent(@event));
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

    private EquatableArray<AttributeSpecification> ReadAttributes(ISymbol symbol)
    {
        if (!_options.IncludeAttributes)
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

    private string? ReadDocumentation(ISymbol symbol)
    {
        if (!_options.IncludeDocumentationComments)
        {
            return null;
        }

        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        List<string> inner = [];
        foreach (var line in Snippet.Dedent(xml!))
        {
            if (!IsMemberTag(line))
            {
                inner.Add(line);
            }
        }

        var body = Snippet.Dedent(string.Join("\n", inner));

        return body.IsEmpty ? null : string.Join("\n", [.. body]);
    }

    private static bool IsMemberTag(string line)
    {
        var trimmed = line.Trim();

        return trimmed.StartsWith("<member ", StringComparison.Ordinal) || trimmed == "</member>";
    }
}

using Microsoft.CodeAnalysis;

namespace Redeclare;

/// <summary>
///     Represents a parameter of a method, constructor, indexer or primary constructor.
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="Name">The name.</param>
/// <param name="Default">The default value, the part after <c>=</c>, or <see langword="null"/> for none.</param>
/// <param name="RefKind">How the parameter is passed: by value, <c>ref</c>, <c>out</c>, <c>in</c>, or <c>ref readonly</c>.</param>
/// <param name="IsParams">Whether this is a <c>params</c> parameter.</param>
/// <param name="IsThis">Whether this is the <c>this</c> parameter of an extension method.</param>
/// <param name="IsScoped">Whether the parameter is <c>scoped</c>. Needs C# 11.</param>
/// <param name="Attributes">The attributes.</param>
internal sealed record ParameterDeclaration(
    TypeReference Type,
    string Name,
    Snippet? Default = null,
    RefKind RefKind = RefKind.None,
    bool IsParams = false,
    bool IsThis = false,
    bool IsScoped = false,
    EquatableArray<AttributeSpecification> Attributes = default);

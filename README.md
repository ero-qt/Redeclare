# Redeclare

A source-only package for Roslyn source generators: symbols in, equatable declaration records out, rendered back to C# under explicit style and language-version options.

The records compare by value, so the incremental pipeline caches on them.

## Install

```sh
dotnet add package Redeclare
```

```powershell
Install-Package Redeclare
```

The package ships sources, not an assembly. They compile into your generator as `internal` types.

The sources are C# 12, so the project needs `<LangVersion>latest</LangVersion>` or a version from 12 up. Without one the build stops at `REDECLARE0001`.

`IsExternalInit`, `InterpolatedStringHandlerAttribute` and `CollectionBuilderAttribute` ship along for frameworks that lack them. If your project already declares them, set `<RedeclarePolyfills>false</RedeclarePolyfills>`.

## The model

Declarations are positional records under `Model/Declarations`. `null` means absent. Lists are `EquatableArray<T>`.

| Record | Declares |
|---|---|
| `CompilationUnit` | A file: header, usings, members. `HintName` names it for `AddSource`. |
| `NamespaceDeclaration` | A namespace. An empty name is the global one and writes no line. |
| `TypeDeclaration` | Classes, structs, interfaces, enums, records and delegates. `ContainingType` gives a nested type its enclosing parts. |
| `ExtensionDeclaration` | A C# 14 extension block. |
| `MethodDeclaration`, `ConstructorDeclaration`, `PropertyDeclaration`, `FieldDeclaration`, `EventDeclaration`, `EnumMemberDeclaration` | Members. Bodies and initializers are `Snippet`s. |
| `RawMemberDeclaration` | Verbatim text for what the model does not express, such as a finalizer. |
| `AttributeSpecification`, `ParameterDeclaration`, `TypeParameterDeclaration`, `AccessorDeclaration` | Parts of the above. |

Type references mirror `ITypeSymbol`: `NamedTypeReference`, `ArrayTypeReference`, `PointerTypeReference`, `FunctionPointerTypeReference`, `TupleTypeReference`, `TypeParameterReference`, `DynamicTypeReference` and `ErrorTypeReference`, each with a `NullableAnnotation`. `ToTypeReference()` reads one from a symbol.

Names follow Roslyn. `Accessibility`, `TypeKind`, `RefKind`, `NullableAnnotation`, `SpecialType` and `VarianceKind` are Roslyn's enums. `Modifiers` is ours, because a generator cannot reference the Workspaces assembly that defines Roslyn's.

## Snippets

A `Snippet` is text with typed holes. An interpolated type reference becomes a hole that renders under the options in force:

```csharp
var body = Snippet.From($$"""
    if ({{comparer}}.Default.Equals({{field.Name}}, value))
    {
        return;
    }

    {{field.Name}} = value;
    """);
```

Format specifiers pin a hole's qualification: `{type:g}` writes `global::`, `{type:f}` the namespace, `{type:m}` the name alone, `{type:n}` the bare identifier. For strings, `{text:L}` writes an escaped literal and `{name:I}` an identifier with `@` when it is a keyword. Raw string literals dedent, and a spliced snippet keeps the indentation of the line it lands on.

## Reading

`ToDeclaration()` reads a type, method, property, field, event, parameter or namespace symbol into the shape the symbol knows: methods without bodies, properties with auto accessors, `partial` only on a partial definition, no `public` on interface members. Operators and conversions read as methods named `operator +` and `implicit operator`. `ToExtensionDeclaration()` reads a C# 14 extension block, `ToConstructorDeclaration()` a constructor and `ToEnumMemberDeclaration()` an enum member. An enum default reads as the member that has the value, and `typeof(List<int>)` in an attribute reads with a hole for the type.

`ReadOptions` decides whether members, attributes, documentation comments and implicitly declared members come along. `ReadOptions.Shape` reads the type alone, which is what a new partial part may repeat.

Attributes are found by class symbol. Resolve the class once with `GetTypeByMetadataName`, then call `GetAttribute` or `GetAttributes`. `GetArguments()` reads constructor and named arguments by name, falling back to a parameter's default when the argument was omitted. `ConstructorExpression` gives an argument back as C#.

`Checks` answers what a generator asks before it acts: `IsPartial`, `IsPartialThroughout`, `Is`, `InheritsFrom`, `Implements`, `FullMetadataName`, and `IsValidOn` for whether an attribute may sit on a symbol.

## Rendering

`RenderOptions` is one record per file: language version, indent, line ending, qualification, predefined type keywords, nullable annotations, namespace style, and expression body preferences per member kind.

`RenderOptions.From(AnalyzerConfigOptions)` reads the consumer's editorconfig and `LanguageVersion.ToCSharpVersion()` their language version, both from providers the pipeline already has:

```csharp
var options = RenderOptions.From(configOptions.GetOptions(tree)) with
{
    Version = ((CSharpParseOptions)parseOptions).LanguageVersion.ToCSharpVersion(),
};
```

Below the version a feature needs, the renderer degrades where C# has an older spelling: a file-scoped namespace becomes a block, `nint` becomes `IntPtr`. Where it has none, such as a `record struct` under C# 9, the renderer writes it anyway and the consumer's compiler reports it, which it does better than a generator can.

Under `Qualification.Minimal`, `CSharpRenderer.CollectNamespaces(unit, options)` returns the usings the file needs for `CompilationUnit.Usings`.

## Usage

A generator that adds a method to every class marked with an attribute:

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class TouchGenerator : IIncrementalGenerator
{
    private static readonly NamedTypeReference _void =
        new(Name: "Void", ContainingNamespace: "System", TypeKind: TypeKind.Struct, SpecialType: SpecialType.System_Void);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider.ForAttributeWithMetadataName(
            "Marks.TouchAttribute",
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => Read((INamedTypeSymbol)ctx.TargetSymbol));

        context.RegisterSourceOutput(types, static (spc, part) =>
        {
            var unit = new CompilationUnit(Members: [part.Namespace with { Members = [part.Type] }], Header: "// <auto-generated/>");
            spc.AddSource(unit.HintName, unit.ToSourceText());
        });
    }

    private sealed record Part(TypeDeclaration Type, NamespaceDeclaration Namespace);

    private static Part Read(INamedTypeSymbol symbol)
    {
        var shape = symbol.ToDeclaration(ReadOptions.Shape);
        var touch = new MethodDeclaration(Accessibility: Accessibility.Public, ReturnType: _void, Name: "Touch", Body: Snippet.Empty);

        return new Part(
            shape with { Modifiers = shape.Modifiers | Modifiers.Partial, Members = [touch] },
            symbol.ContainingNamespace.ToDeclaration());
    }
}
```

The transform is the last place a symbol appears, so an edit elsewhere in the consumer's project compares equal and the output stage does not run.

## Extensions

`Redeclare.Extensions` is a second source-only package for the parts a generator would otherwise write itself. Reference both, since a source-only package's files reach only the project that references it directly.

```sh
dotnet add package Redeclare.Extensions
```

It holds the types C# has a keyword for, so `TypeReference.Int32` replaces constructing one. `ToPart()` reads a type as a new partial part of itself, and `WithCollectedUsings(options)` fills a file's usings for minimal qualification.

`RenderOptions.From(parseOptions)` is the defaults at the consumer's language version, and `RenderOptions.From(config, parseOptions)` their editorconfig style at that version. `WithRenderOptions` meets each item with the style of the file it came from. Roslyn's `Combine` takes one provider and returns `(Left, Right)`, so this is the editorconfig and the language version reached by hand:

```csharp
var style = context.AnalyzerConfigOptionsProvider.Combine(context.ParseOptionsProvider);

var enums = context.SyntaxProvider
    .ForAttributeWithMetadataName(name, predicate, static (ctx, _) => (Info: Read(ctx), Tree: ctx.TargetNode.SyntaxTree))
    .Combine(style)
    .Select(static (pair, _) => pair.Left.Info with
    {
        Options = RenderOptions.From(pair.Right.Left.GetOptions(pair.Left.Tree), pair.Right.Right),
    });
```

and this is the same thing:

```csharp
var enums = context.SyntaxProvider
    .ForAttributeWithMetadataName(name, predicate, static (ctx, _) => (Read(ctx), ctx.TargetNode.SyntaxTree))
    .WithRenderOptions(context, static (info, options) => info with { Options = options });
```

## Samples

You can find three sample generators under `samples/`, each with a consumer project that only compiles when the generator ran:

- `EnumHelpers` gives an enum `ToStringFast`, `IsDefined` and `TryParse`, in the consumer's editorconfig style.
- `Notify` gives a field a change-notifying property and its class `INotifyPropertyChanged`.
- `Implement` fills in partial members from their definitions under minimal qualification.

## License

MIT.

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
| `CompilationUnit` | A file: header, `#nullable` and `#pragma warning disable` directives, usings, members. `HintName` names it for `AddSource`. |
| `NamespaceDeclaration` | A namespace. An empty name is the global one and writes no line. |
| `TypeDeclaration` | Classes, structs, interfaces, enums and records. `ContainingType` gives a nested type its enclosing parts. |
| `DelegateDeclaration` | A delegate: return type, name and parameters. |
| `ExtensionDeclaration` | A C# 14 extension block. |
| `MethodDeclaration`, `ConstructorDeclaration`, `PropertyDeclaration`, `FieldDeclaration`, `EventDeclaration`, `EnumMemberDeclaration` | Members. Bodies and initializers are `Snippet`s. |
| `RawMemberDeclaration` | Verbatim text for what the model does not express, such as a finalizer. |
| `AttributeSpecification`, `ParameterDeclaration`, `TypeParameterDeclaration`, `AccessorDeclaration` | Parts of the above. |

Type references mirror `ITypeSymbol`: `NamedTypeReference`, `ArrayTypeReference`, `PointerTypeReference`, `FunctionPointerTypeReference`, `TupleTypeReference`, `TypeParameterReference`, `DynamicTypeReference` and `ErrorTypeReference`, each with a `NullableAnnotation`. `ToTypeReference()` reads one from a symbol.

Names follow Roslyn. `Accessibility`, `TypeKind`, `RefKind`, `NullableAnnotation`, `SpecialType` and `VarianceKind` are Roslyn's enums. `Modifiers` is ours, because a generator cannot reference the Workspaces assembly that defines Roslyn's.

## Snippets

A `Snippet` is text with typed holes. Interpolate a `TypeReference` and it becomes a hole that renders under the file's options:

```csharp
var body = Snippet.From($$"""
    if ({{comparer}}.Default.Equals({{field.Name}}, value))
    {
        return;
    }

    {{field.Name}} = value;
    """);
```

A format specifier pins how a hole spells its type. Two more cover strings:

```csharp
Snippet.From($"{list:g} x;");        // global::System.Collections.Generic.List<int> x;
Snippet.From($"{list:f} x;");        // System.Collections.Generic.List<int> x;
Snippet.From($"{list:m} x;");        // List<int> x;
Snippet.From($"{list:n}Helper");     // ListHelper
Snippet.From($"return {text:L};");   // return "a \"quoted\" string";
Snippet.From($"var {name:I} = 1;");  // var @class = 1;
```

Raw string literals dedent. A snippet spliced into another keeps the indentation of the line it lands on, so arms and members line up:

```csharp
var arms = Snippet.Join("\n", members.Select(m => Snippet.From($"{{type}}.{m.Name} => \"{m.Name}\",")));
var body = Snippet.From($$"""
    return value switch
    {
        {{arms}}
        _ => value.ToString(),
    };
    """);
```

## Reading

Any symbol with a declaration behind it has `ToDeclaration()`: `INamedTypeSymbol`, `IMethodSymbol`, `IPropertySymbol`, `IFieldSymbol`, `IEventSymbol`, `IParameterSymbol` and `INamespaceSymbol`. Methods come back without bodies, properties with auto accessors, interface members without `public`, and `partial` only on the definition half of a partial member.

```csharp
var type = symbol.ToDeclaration();                         // TypeDeclaration, DelegateDeclaration or ExtensionDeclaration
var method = methodSymbol.ToDeclaration();                 // MethodDeclaration, body left null
var constructor = ctorSymbol.ToConstructorDeclaration();   // an IMethodSymbol, so it needs its own name
var value = fieldSymbol.ToEnumMemberDeclaration();         // an IFieldSymbol, same reason
var member = ((ISymbol)ctorSymbol).ToDeclaration();        // MemberDeclaration, picked by kind: here a ConstructorDeclaration
```

When you know what kind of named type you have, `symbol.ToTypeDeclaration()`, `symbol.ToDelegateDeclaration()` and `symbol.ToExtensionDeclaration()` give that record back.

Operators, conversions and destructors are `MethodDeclaration`s too, and their `Name` is a `MethodName` that says which. An enum default reads as the member with that value. `typeof(List<int>)` in an attribute reads with a hole for the type.

`ReadOptions` says what comes along: members, attributes, documentation comments, implicitly declared members. `ReadOptions.Shape` reads a type alone, for a new partial part. `ReadOptions.Signature` reads a member without attributes or documentation, for an implementing part.

```csharp
var part = symbol.ToTypeDeclaration(ReadOptions.Shape);
var definition = propertySymbol.ToDeclaration(ReadOptions.Signature);
```

Attributes are found by class symbol. Resolve the class once, then ask a symbol for it:

```csharp
var attribute = compilation.GetTypeByMetadataName("My.MarkAttribute")!;
var arguments = symbol.GetAttribute(attribute)?.GetArguments();   // by name, with the parameter's default when left out
var name = arguments?.GetConstructorExpression("name");           // the argument back as C#
```

Symbols also answer the questions a generator asks before it acts: `type.IsPartial()`, `type.IsPartialThroughout()`, `type.Is(other)`, `type.InheritsFrom(other)`, `type.Implements(other)`, `type.GetFullMetadataName()`, and `attribute.IsValidOn(symbol, compilation)`.

## Rendering

`RenderOptions` is one record per file: language version, indent, line ending, qualification, predefined type keywords, nullable annotations, namespace style, and expression body preferences per member kind.

```csharp
var text = unit.Render(options);
context.AddSource(unit.HintName, unit.ToSourceText(options));
```

The consumer's editorconfig and language version both come from providers the pipeline already has:

```csharp
var options = RenderOptions.From(configOptions.GetOptions(tree)) with
{
    Version = ((CSharpParseOptions)parseOptions).LanguageVersion.ToCSharpVersion(),
};
```

Below the version a feature needs, the renderer falls back where C# has an older spelling: a file-scoped namespace becomes a block, `nint` becomes `IntPtr`. Where it has none, such as a `record struct` under C# 9, the renderer writes it anyway and the consumer's compiler reports it.

Under `Qualification.Minimal` the file needs usings. The renderer knows which:

```csharp
var usings = CSharpRenderer.CollectNamespaces(unit, options);
var text = (unit with { Usings = [.. usings] }).Render(options);
```

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

It holds the types C# has a keyword for, a reader for partial parts, the usings step from above as one call, and the argument list that forwards a call:

```csharp
var count = new FieldDeclaration(Type: TypeReference.Int32, Name: "_count");
var part = symbol.ToPart();                    // a new partial part of the type, no members
var file = unit.WithCollectedUsings(options);  // usings filled for minimal qualification
var call = Snippet.From($"Inner({method.Parameters.ToArguments()});");  // Inner(ref a, out b, c)
```

It also reads the consumer's language version straight from `ParseOptions`, alone or together with the editorconfig:

```csharp
var defaults = RenderOptions.From(parseOptions);
var style = RenderOptions.From(configOptions.GetOptions(tree), parseOptions);
```

Roslyn's `Combine` takes one provider and returns `(Left, Right)`, so reaching both by hand looks like this:

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

`WithRenderOptions` does the same and meets each item with the style of the file it came from:

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

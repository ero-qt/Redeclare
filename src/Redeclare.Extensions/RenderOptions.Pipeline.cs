using Microsoft.CodeAnalysis;
using System;

namespace Redeclare;

/// <summary>
///     Provides the one pipeline step nearly every generator needs: each item paired with the render options for the
///     file it came from. <c>Combine</c> takes a single provider and returns a <c>(Left, Right)</c> pair, so reaching
///     the editorconfig and the language version by hand nests two levels deep and is read back out as
///     <c>pair.Right.Left</c>. This does that once, here.
/// </summary>
internal static class RenderOptionsPipeline
{
    /// <summary>
    ///     Each item with the options its own file is rendered under, folded together by <paramref name="select"/>.
    ///     Editorconfig values are per file, which is why the items carry the tree they were read from.
    /// </summary>
    /// <typeparam name="TSource">The item the transform produced.</typeparam>
    /// <typeparam name="TResult">What <paramref name="select"/> returns.</typeparam>
    /// <param name="source">Items paired with the tree each was read from.</param>
    /// <param name="context">The initialization context, for the editorconfig and parse options providers.</param>
    /// <param name="select">Folds an item and its options into the value the output stage receives.</param>
    /// <returns>A provider of the folded values, which holds no syntax tree and so compares by value.</returns>
    public static IncrementalValuesProvider<TResult> WithRenderOptions<TSource, TResult>(
        this IncrementalValuesProvider<(TSource Value, SyntaxTree Tree)> source,
        IncrementalGeneratorInitializationContext context,
        Func<TSource, RenderOptions, TResult> select)
    {
        var style = context.AnalyzerConfigOptionsProvider.Combine(context.ParseOptionsProvider);

        return source.Combine(style).Select((pair, _) =>
        {
            var ((value, tree), (config, parseOptions)) = pair;

            return select(value, RenderOptions.From(config.GetOptions(tree), parseOptions));
        });
    }
}

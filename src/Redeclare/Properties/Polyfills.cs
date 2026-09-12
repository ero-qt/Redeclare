// Defines the compiler markers netstandard2.0 lacks. Records and `init` need `IsExternalInit`. A collection
// expression into `EquatableArray<T>` needs `CollectionBuilderAttribute` to find its builder. `Snippet.From($"...")`
// only reaches its handler when `InterpolatedStringHandlerAttribute` marks it. None of the three does anything at
// run time, so shipping them costs a consumer nothing.
#if !REDECLARE_EXCLUDE_POLYFILLS

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

#if !NET5_0_OR_GREATER
/// <summary>
///     Marks an <c>init</c> accessor. The compiler requires the type to exist and uses none of its members.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class IsExternalInit { }
#endif

#if !NET6_0_OR_GREATER
/// <summary>
///     Marks a type as an interpolated string handler, the shape <c>Snippet.From($"...")</c> binds to.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class InterpolatedStringHandlerAttribute : Attribute { }
#endif

#if !NET8_0_OR_GREATER
/// <summary>
///     Names the method a collection expression calls to build a collection type.
/// </summary>
/// <param name="builderType">The type holding the builder method.</param>
/// <param name="methodName">The name of the builder method, which takes a <c>ReadOnlySpan&lt;T&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class CollectionBuilderAttribute(
    Type builderType,
    string methodName)
    : Attribute
{
    /// <summary>
    ///     Gets the type holding the builder method.
    /// </summary>
    public Type BuilderType { get; } = builderType;

    /// <summary>
    ///     Gets the name of the builder method.
    /// </summary>
    public string MethodName { get; } = methodName;
}
#endif

#endif

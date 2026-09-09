// netstandard2.0 has no CollectionBuilderAttribute, and without one a collection expression into
// EquatableArray<T> cannot find its builder. The marker has no behaviour, so shipping it costs a consumer nothing.
#if !REDECLARE_NO_POLYFILLS

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

#if !NET8_0_OR_GREATER
/// <summary>
///     Names the method a collection expression calls to build a collection type.
/// </summary>
/// <param name="builderType">The type holding the builder method.</param>
/// <param name="methodName">The name of the builder method, which takes a <c>ReadOnlySpan&lt;T&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class CollectionBuilderAttribute(Type builderType, string methodName) : Attribute
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

using System;

namespace Redeclare;

/// <summary>
///     Specifies the modifiers a declaration carries besides its accessibility. The renderer writes them in
///     the order the C# style guidelines and the compiler expect, whatever order they were set in.
/// </summary>
/// <remarks>
///     Accessibility is not a flag here. It is the <c>Accessibility</c> property of the declaration, which uses
///     Roslyn's enum of that name.
/// </remarks>
[Flags]
internal enum Modifiers
{
    /// <summary>
    ///     No modifiers.
    /// </summary>
    None = 0,

    /// <summary>
    ///     <c>const</c>. Implies static, so it is not combined with <see cref="Static"/>.
    /// </summary>
    Const = 1 << 0,

    /// <summary>
    ///     <c>static</c>.
    /// </summary>
    Static = 1 << 1,

    /// <summary>
    ///     <c>extern</c>.
    /// </summary>
    Extern = 1 << 2,

    /// <summary>
    ///     <c>new</c>, hiding an inherited member.
    /// </summary>
    New = 1 << 3,

    /// <summary>
    ///     <c>virtual</c>.
    /// </summary>
    Virtual = 1 << 4,

    /// <summary>
    ///     <c>abstract</c>.
    /// </summary>
    Abstract = 1 << 5,

    /// <summary>
    ///     <c>sealed</c>.
    /// </summary>
    Sealed = 1 << 6,

    /// <summary>
    ///     <c>override</c>.
    /// </summary>
    Override = 1 << 7,

    /// <summary>
    ///     <c>readonly</c>, on a field, a struct, or a struct member.
    /// </summary>
    ReadOnly = 1 << 8,

    /// <summary>
    ///     <c>unsafe</c>.
    /// </summary>
    Unsafe = 1 << 9,

    /// <summary>
    ///     <c>required</c>, on a field or property. Needs C# 11.
    /// </summary>
    Required = 1 << 10,

    /// <summary>
    ///     <c>volatile</c>.
    /// </summary>
    Volatile = 1 << 11,

    /// <summary>
    ///     <c>async</c>.
    /// </summary>
    Async = 1 << 12,

    /// <summary>
    ///     <c>partial</c>. Rendered last, where the language requires it.
    /// </summary>
    Partial = 1 << 13,

    /// <summary>
    ///     <c>ref</c>, on a struct. Rendered just before <c>partial</c> and the type keyword. Needs C# 7.2.
    /// </summary>
    Ref = 1 << 14,

    /// <summary>
    ///     <c>file</c>, on a type. Needs C# 11.
    /// </summary>
    File = 1 << 15,
}

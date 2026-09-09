using System.Diagnostics.CodeAnalysis;

// These sources compile into the consuming generator under the consumer's analyzer settings, so a rule that
// fires by construction is suppressed here, where every consumer inherits it.
[assembly: SuppressMessage(
    "Design",
    "CA1064:Exceptions should be public",
    Justification = "Source-only library; every type is internal so nothing leaks from the generator assembly.",
    Scope = "type",
    Target = "~T:Redeclare.RenderException")]

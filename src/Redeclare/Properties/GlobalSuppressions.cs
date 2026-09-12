using System.Diagnostics.CodeAnalysis;

// Suppresses the rules these sources break on purpose. They compile inside the consumer's generator under the
// consumer's analyzer settings, and every consumer inherits what is written here.
[assembly: SuppressMessage(
    "Design",
    "CA1064:Exceptions should be public",
    Justification = "Source-only library; every type is internal so nothing leaks from the generator assembly.",
    Scope = "type",
    Target = "~T:Redeclare.RenderException")]

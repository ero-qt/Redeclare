using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Redeclare;

/// <summary>
///     Provides methods that read the MSBuild properties and item metadata a project forwards to analyzers
///     through <c>CompilerVisibleProperty</c> and <c>CompilerVisibleItemMetadata</c>. They arrive in
///     <c>AnalyzerConfigOptions</c> as <c>build_property.Name</c> and <c>build_metadata.ItemType.Name</c>, and
///     these methods know the prefixes and the conversions.
/// </summary>
/// <remarks>
///     Properties come from <c>provider.GlobalOptions</c>, item metadata from
///     <c>provider.GetOptions(additionalText)</c> for the item in question. Values are text, so a property left
///     empty and a property absent both read as <see langword="null"/>.
/// </remarks>
internal static class AnalyzerConfigOptionsExtensions
{
    /// <summary>
    ///     Gets the value of <c>build_property.<paramref name="name"/></c>, or <see langword="null"/> when it is absent
    ///     or empty.
    /// </summary>
    public static string? MSBuildProperty(this AnalyzerConfigOptions options, string name)
    {
        return options.TryGetValue("build_property." + name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    /// <summary>
    ///     Gets the value of <c>build_metadata.<paramref name="itemType"/>.<paramref name="name"/></c>, or
    ///     <see langword="null"/>.
    /// </summary>
    public static string? MSBuildMetadata(this AnalyzerConfigOptions options, string itemType, string name)
    {
        return options.TryGetValue("build_metadata." + itemType + "." + name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    /// <summary>
    ///     Gets a property as MSBuild reads booleans: <c>true</c> or <c>false</c> in any casing, or
    ///     <see langword="null"/> otherwise.
    /// </summary>
    public static bool? MSBuildBoolean(this AnalyzerConfigOptions options, string name)
    {
        return options.MSBuildProperty(name) is { } value && bool.TryParse(value, out bool result) ? result : null;
    }

    /// <summary>
    ///     Gets a property as an integer, or <see langword="null"/> when it is absent or not a number.
    /// </summary>
    public static int? MSBuildInt32(this AnalyzerConfigOptions options, string name)
    {
        return options.MSBuildProperty(name) is { } value
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : null;
    }

    /// <summary>
    ///     Gets a property as an enum member by name, in any casing, or <see langword="null"/>.
    /// </summary>
    public static TEnum? MSBuildEnum<TEnum>(this AnalyzerConfigOptions options, string name)
        where TEnum : struct, Enum
    {
        return options.MSBuildProperty(name) is { } value && Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;
    }

    /// <summary>
    ///     Gets a property that is an MSBuild list, split on <c>;</c> with blanks dropped and entries trimmed. The list
    ///     is empty when the property is absent.
    /// </summary>
    public static EquatableArray<string> MSBuildList(this AnalyzerConfigOptions options, string name)
    {
        if (options.MSBuildProperty(name) is not { } value)
        {
            return default;
        }

        var parts = value.Split(';');
        List<string> kept = new(parts.Length);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                kept.Add(trimmed);
            }
        }

        return kept.ToEquatableArray();
    }
}

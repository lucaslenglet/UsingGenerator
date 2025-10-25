using System;

namespace UsingGenerator;

/// <summary>
/// A single <c>global using</c> directive to emit, as declared by a <c>SharedUsing</c> attribute.
/// </summary>
internal readonly record struct UsingSpec(string Target, bool Static, string? Alias)
    : IComparable<UsingSpec>
{
    /// <summary>
    /// The rendered directive. Computed once: it is also the sort and dedup key, so a
    /// <c>SortedSet</c> would otherwise format it on every comparison.
    /// </summary>
    public string Text { get; } = Alias is not null
        ? $"global using {Alias} = {Target};"
        : Static
            ? $"global using static {Target};"
            : $"global using {Target};";

    public int CompareTo(UsingSpec other) => string.CompareOrdinal(Text, other.Text);
}

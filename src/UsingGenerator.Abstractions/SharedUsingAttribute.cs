using System;

namespace UsingGenerator;

/// <summary>
/// Declares one import carried by a <see cref="UsingBundleAttribute"/> subclass. The import is
/// emitted as a <c>global using</c> in every assembly applying that bundle.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SharedUsingAttribute : Attribute
{
    /// <param name="namespace">
    /// The namespace to import, or the fully qualified type name when <see cref="Static"/>
    /// is <see langword="true"/> or when <see cref="Alias"/> targets a type.
    /// </param>
    public SharedUsingAttribute(string @namespace)
    {
        Namespace = @namespace;
    }

    /// <summary>
    /// The namespace or type name being imported.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Emits <c>global using static</c> instead of a plain import.
    /// </summary>
    public bool Static { get; set; }

    /// <summary>
    /// Emits <c>global using {Alias} = {Namespace};</c> when set.
    /// </summary>
    public string? Alias { get; set; }
}

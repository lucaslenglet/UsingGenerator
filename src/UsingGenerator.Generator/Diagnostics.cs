using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace UsingGenerator;

internal static class Diagnostics
{
    private const string Category = "UsingGenerator";

    /// <summary>
    /// <c>global using X = Y;</c> and <c>global using static Y;</c> are mutually exclusive forms.
    /// </summary>
    public static readonly DiagnosticDescriptor AliasWithStatic = new(
        id: "UG0001",
        title: "Alias cannot be combined with Static",
        messageFormat: "Import '{0}' sets both Alias and Static, which is not a valid using directive",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyTarget = new(
        id: "UG0002",
        title: "Import target is empty",
        messageFormat: "An import declared by '{0}' has an empty target and is ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SharedUsingOutsideBundle = new(
        id: "UG0003",
        title: "SharedUsing is only honored on a bundle",
        messageFormat: "'{0}' declares imports but does not derive from UsingBundleAttribute, so they are ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}

/// <summary>
/// A source position stored as plain data.
/// </summary>
/// <remarks>
/// A <see cref="Location"/> holds its <see cref="SyntaxTree"/>, which every edit replaces. Keeping
/// one in a pipeline value would both pin the compilation in memory and make the value compare
/// unequal on every keystroke, defeating the caching the pipeline is built around.
/// </remarks>
internal readonly record struct LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationInfo? From(Location? location) =>
        location?.SourceTree is null
            ? null
            : new LocationInfo(
                location.SourceTree.FilePath,
                location.SourceSpan,
                location.GetLineSpan().Span);

    public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}

/// <summary>
/// A diagnostic held in an incremental pipeline. <see cref="Diagnostic"/> itself is unsuitable
/// there: it holds a symbol graph alive and does not compare structurally.
/// </summary>
internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    string Argument)
{
    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), Argument);
}

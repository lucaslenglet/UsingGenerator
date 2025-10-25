using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace UsingGenerator;

/// <summary>
/// Emits the <c>global using</c> directives carried by the <see cref="UsingBundleAttribute"/>
/// subclasses an assembly applies to itself.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SharedUsingGenerator : IIncrementalGenerator
{
    private const string AttributeNamespace = "UsingGenerator";
    private const string SharedUsingAttributeName = "SharedUsingAttribute";
    private const string BundleAttributeName = "UsingBundleAttribute";
    private const string HintName = "SharedUsings.g.cs";

    /// <summary>
    /// Names the collection step so tests can assert it stays cached across unrelated edits.
    /// </summary>
    public const string CollectStepName = "Collect";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // CompilationProvider is invalidated on every keystroke, so the result is reduced to
        // structurally comparable values right away: downstream stays cached unless they change.
        IncrementalValueProvider<CollectedUsings> collected = context.CompilationProvider
            .Select(Collect)
            .WithTrackingName(CollectStepName);

        context.RegisterSourceOutput(collected.Select(static (c, _) => c.Specs), Emit);
        context.RegisterSourceOutput(collected.SelectMany(static (c, _) => c.Diagnostics), Report);

        // Independent of the above: catches [SharedUsing] placed on a class that is not a bundle,
        // which would otherwise be silently ignored. The check runs inside the transform so that
        // no symbol survives into the pipeline, where it would pin the compilation in memory.
        IncrementalValuesProvider<DiagnosticInfo> misplaced = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeNamespace + "." + SharedUsingAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => ctx.TargetSymbol is INamedTypeSymbol type && !IsBundle(type)
                    ? new DiagnosticInfo(
                        Diagnostics.SharedUsingOutsideBundle,
                        LocationInfo.From(FirstLocation(type)),
                        type.Name)
                    : (DiagnosticInfo?)null)
            .Where(static diagnostic => diagnostic is not null)
            .Select(static (diagnostic, _) => diagnostic!.Value);

        context.RegisterSourceOutput(misplaced, Report);
    }

    private static CollectedUsings Collect(Compilation compilation, CancellationToken cancellationToken)
    {
        SortedSet<UsingSpec> specs = [];
        List<DiagnosticInfo> diagnostics = [];

        // Applying a bundle is the only way in: nothing is imported, and nothing crosses an
        // assembly reference, unless this assembly carries the bundle attribute itself.
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            CollectBundle(attribute.AttributeClass);
        }

        return new CollectedUsings(
            new EquatableArray<UsingSpec>([.. specs]),
            new EquatableArray<DiagnosticInfo>([.. diagnostics]));

        // Reads the imports carried by a bundle, walking its whole base chain so that bundles
        // compose by inheritance. Everything read here lives in metadata, which is what lets a
        // bundle declared in a referenced assembly still be resolved.
        void CollectBundle(INamedTypeSymbol? attributeClass)
        {
            if (!IsBundle(attributeClass))
            {
                return;
            }

            for (INamedTypeSymbol? type = attributeClass;
                 type is not null && !Is(type, BundleAttributeName);
                 type = type.BaseType)
            {
                foreach (AttributeData attribute in type.GetAttributes())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!Is(attribute.AttributeClass, SharedUsingAttributeName))
                    {
                        continue;
                    }

                    if (ReadSpec(attribute, type, out DiagnosticInfo? diagnostic) is { } spec)
                    {
                        specs.Add(spec);
                    }

                    if (diagnostic is { } info)
                    {
                        diagnostics.Add(info);
                    }
                }
            }
        }
    }

    private static bool IsBundle(INamedTypeSymbol? attributeClass)
    {
        for (INamedTypeSymbol? type = attributeClass?.BaseType; type is not null; type = type.BaseType)
        {
            if (Is(type, BundleAttributeName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Matches by name rather than by symbol identity, so that a bundle still resolves when the
    /// attributes assembly is reachable only through a reference, or is present in two versions.
    /// </summary>
    private static bool Is(INamedTypeSymbol? type, string name) =>
        type is { ContainingNamespace: { Name: AttributeNamespace, ContainingNamespace.IsGlobalNamespace: true } }
        && type.Name == name;

    private static UsingSpec? ReadSpec(
        AttributeData attribute,
        INamedTypeSymbol bundle,
        out DiagnosticInfo? diagnostic)
    {
        diagnostic = null;

        ImmutableArray<TypedConstant> arguments = attribute.ConstructorArguments;

        // List patterns are avoided here: they require System.Index, which netstandard2.0 lacks.
        if (arguments.Length != 1 || arguments[0].Value is not string target)
        {
            return null;
        }

        // A bundle read from metadata has no syntax to point at; the declaring assembly is the
        // one that should have reported it.
        LocationInfo? location = LocationInfo.From(
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? FirstLocation(bundle));

        target = target.Trim();

        if (target.Length == 0)
        {
            diagnostic = new DiagnosticInfo(Diagnostics.EmptyTarget, location, bundle.Name);
            return null;
        }

        bool isStatic = false;
        string? alias = null;

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            switch (argument.Key)
            {
                case nameof(UsingSpec.Static) when argument.Value.Value is bool value:
                    isStatic = value;
                    break;

                case nameof(UsingSpec.Alias) when argument.Value.Value is string value:
                    alias = value.Trim() is { Length: > 0 } trimmed ? trimmed : null;
                    break;
            }
        }

        if (isStatic && alias is not null)
        {
            diagnostic = new DiagnosticInfo(Diagnostics.AliasWithStatic, location, target);
            return null;
        }

        return new UsingSpec(target, isStatic, alias);
    }

    private static Location? FirstLocation(ISymbol symbol) =>
        symbol.Locations.Length > 0 ? symbol.Locations[0] : null;

    private static void Report(SourceProductionContext context, DiagnosticInfo diagnostic) =>
        context.ReportDiagnostic(diagnostic.ToDiagnostic());

    private static void Emit(SourceProductionContext context, EquatableArray<UsingSpec> specs)
    {
        if (specs.Count == 0)
        {
            return;
        }

        StringBuilder builder = new StringBuilder()
            .AppendLine("// <auto-generated/>")
            .AppendLine("#nullable enable");

        foreach (UsingSpec spec in specs)
        {
            builder.AppendLine(spec.Text);
        }

        context.AddSource(HintName, SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private readonly record struct CollectedUsings(
        EquatableArray<UsingSpec> Specs,
        EquatableArray<DiagnosticInfo> Diagnostics);
}

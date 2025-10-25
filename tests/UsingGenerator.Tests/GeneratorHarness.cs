using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UsingGenerator.Tests;

/// <summary>
/// Runs the generator over a snippet of source and exposes what it produced.
/// </summary>
internal static class GeneratorHarness
{
    /// <summary>
    /// The framework the test host runs on. Taken from the host rather than from whatever happens
    /// to be loaded, so the reference set does not depend on test execution order.
    /// </summary>
    private static readonly ImmutableArray<MetadataReference> References =
    [
        .. ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(System.IO.Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),

        MetadataReference.CreateFromFile(typeof(UsingBundleAttribute).Assembly.Location),
    ];

    public static CSharpCompilation Compile(string source) => CSharpCompilation.Create(
        assemblyName: "Tests.Generated",
        syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
        references: References,
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>
    /// A driver that records why each step ran, for incremental caching assertions.
    /// </summary>
    public static GeneratorDriver CreateDriver() => CSharpGeneratorDriver.Create(
        [new SharedUsingGenerator().AsSourceGenerator()],
        driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

    public static GeneratorResult Run(string source)
    {
        GeneratorDriverRunResult result = CreateDriver().RunGenerators(Compile(source)).GetRunResult();

        GeneratedSourceResult? generated = result.Results
            .SelectMany(r => r.GeneratedSources)
            .Cast<GeneratedSourceResult?>()
            .SingleOrDefault();

        return new GeneratorResult(generated, result.Diagnostics);
    }
}

internal sealed record GeneratorResult(
    GeneratedSourceResult? Generated,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public bool EmittedNothing => Generated is null;

    public string[] DiagnosticIds => [.. Diagnostics.Select(d => d.Id)];

    /// <summary>
    /// The emitted directives, read from the syntax tree the driver already produced rather than
    /// from the raw text, so the assertions do not depend on formatting.
    /// </summary>
    public string[] Usings =>
    [
        .. Generated?.SyntaxTree.GetCompilationUnitRoot().Usings.Select(u => u.ToString()) ?? [],
    ];
}

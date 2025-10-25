using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace UsingGenerator.Tests;

/// <summary>
/// The collection step reads the whole <see cref="Compilation"/>, which Roslyn invalidates on
/// every keystroke. Without structural equality on its output, every edit in the IDE would
/// re-run the generator and re-emit the file.
/// </summary>
public class IncrementalCachingTests
{
    private const string Bundle = """
        using UsingGenerator;

        [assembly: Bundle]

        [SharedUsing("System.Linq")]
        public sealed class BundleAttribute : UsingBundleAttribute;
        """;

    private const string BundleWithDiagnostic = """
        using UsingGenerator;

        [assembly: Bundle]

        [SharedUsing("   ")]
        public sealed class BundleAttribute : UsingBundleAttribute;
        """;

    [Fact]
    public void An_edit_that_changes_no_import_keeps_the_output_cached()
    {
        CSharpCompilation compilation = GeneratorHarness.Compile(Bundle);
        GeneratorDriver driver = GeneratorHarness.CreateDriver().RunGenerators(compilation);

        // An edit anywhere else in the assembly: the imports are untouched.
        CSharpCompilation edited = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("public sealed class Unrelated { public int Value = 1; }"));

        driver = driver.RunGenerators(edited);

        IncrementalGeneratorRunStep[] steps =
        [
            .. driver.GetRunResult().Results
                .SelectMany(result => result.TrackedSteps[SharedUsingGenerator.CollectStepName]),
        ];

        Assert.NotEmpty(steps);
        Assert.All(
            steps.SelectMany(step => step.Outputs),
            output => Assert.Equal(IncrementalStepRunReason.Unchanged, output.Reason));
    }

    /// <summary>
    /// Diagnostics travel in the same pipeline value as the imports. Holding a Roslyn
    /// <see cref="Location"/> there would compare unequal on every edit, so a single misconfigured
    /// bundle would silently disable caching for the whole generator.
    /// </summary>
    [Fact]
    public void A_reported_diagnostic_does_not_break_caching()
    {
        CSharpCompilation compilation = GeneratorHarness.Compile(BundleWithDiagnostic);
        GeneratorDriver driver = GeneratorHarness.CreateDriver().RunGenerators(compilation);

        // An edit inside the very file the diagnostic points at, changing nothing it reports on.
        // This is what a keystroke does: it replaces the syntax tree instance.
        SyntaxTree original = compilation.SyntaxTrees.Single();
        CSharpCompilation edited = compilation.ReplaceSyntaxTree(
            original,
            CSharpSyntaxTree.ParseText(BundleWithDiagnostic + Environment.NewLine + "// edited"));

        driver = driver.RunGenerators(edited);

        IncrementalGeneratorRunStep[] steps =
        [
            .. driver.GetRunResult().Results
                .SelectMany(result => result.TrackedSteps[SharedUsingGenerator.CollectStepName]),
        ];

        Assert.NotEmpty(steps);
        Assert.All(
            steps.SelectMany(step => step.Outputs),
            output => Assert.Equal(IncrementalStepRunReason.Unchanged, output.Reason));
    }
}

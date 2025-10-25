using Xunit;

namespace UsingGenerator.Tests;

public class SharedUsingGeneratorTests
{
    [Fact]
    public void Applied_bundle_emits_its_imports()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using UsingGenerator;

            [assembly: Bundle]

            [SharedUsing("System.Linq")]
            [SharedUsing("System.Console", Static = true)]
            [SharedUsing("System.Collections.Generic.List<string>", Alias = "StringList")]
            public sealed class BundleAttribute : UsingBundleAttribute;
            """);

        Assert.Equal(
            [
                "global using StringList = System.Collections.Generic.List<string>;",
                "global using System.Linq;",
                "global using static System.Console;",
            ],
            result.Usings);
    }

    [Fact]
    public void Declared_but_unapplied_bundle_emits_nothing()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using UsingGenerator;

            [SharedUsing("System.Linq")]
            public sealed class BundleAttribute : UsingBundleAttribute;
            """);

        Assert.True(result.EmittedNothing);
    }

    [Fact]
    public void Bundle_inherits_the_imports_of_its_base()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using UsingGenerator;

            [assembly: Derived]

            [SharedUsing("System.Linq")]
            public class BaseBundleAttribute : UsingBundleAttribute;

            [SharedUsing("System.Text")]
            public sealed class DerivedAttribute : BaseBundleAttribute;
            """);

        Assert.Equal(["global using System.Linq;", "global using System.Text;"], result.Usings);
    }

    [Fact]
    public void Duplicate_imports_are_emitted_once()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using UsingGenerator;

            [assembly: First]
            [assembly: Second]

            [SharedUsing("System.Linq")]
            public sealed class FirstAttribute : UsingBundleAttribute;

            [SharedUsing("System.Linq")]
            public sealed class SecondAttribute : UsingBundleAttribute;
            """);

        Assert.Equal(["global using System.Linq;"], result.Usings);
    }

    [Fact]
    public void Alias_combined_with_static_is_reported_and_skipped()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using UsingGenerator;

            [assembly: Bundle]

            [SharedUsing("System.Console", Static = true, Alias = "Out")]
            [SharedUsing("System.Linq")]
            public sealed class BundleAttribute : UsingBundleAttribute;
            """);

        Assert.Equal(["UG0001"], result.DiagnosticIds);
        Assert.Equal(["global using System.Linq;"], result.Usings);
    }

    [Fact]
    public void Empty_target_is_reported_and_skipped()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using UsingGenerator;

            [assembly: Bundle]

            [SharedUsing("   ")]
            public sealed class BundleAttribute : UsingBundleAttribute;
            """);

        Assert.Equal(["UG0002"], result.DiagnosticIds);
        Assert.True(result.EmittedNothing);
    }

    [Fact]
    public void SharedUsing_on_a_non_bundle_class_is_reported()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using UsingGenerator;

            [SharedUsing("System.Linq")]
            public sealed class NotABundle;
            """);

        Assert.Equal(["UG0003"], result.DiagnosticIds);
        Assert.True(result.EmittedNothing);
    }

    [Fact]
    public void Nothing_is_inherited_without_applying_a_bundle()
    {
        GeneratorResult result = GeneratorHarness.Run("""
            using System;

            public sealed class Unrelated;
            """);

        Assert.True(result.EmittedNothing);
        Assert.Empty(result.DiagnosticIds);
    }
}

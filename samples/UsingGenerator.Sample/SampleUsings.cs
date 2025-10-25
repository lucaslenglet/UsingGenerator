using UsingGenerator.Sample;

// Imports specific to this assembly still go through a bundle: declare it, then apply it.
[assembly: SampleUsings]

namespace UsingGenerator.Sample;

[SharedUsing("System.Collections.Generic.List<string>", Alias = "StringList")]
public sealed class SampleUsingsAttribute : UsingBundleAttribute;

namespace UsingGenerator.Sample.Library;

/// <summary>
/// A reusable bundle. This library declares it and applies nothing to itself: an assembly
/// referencing this one gets these imports by writing <c>[assembly: DefaultUsings]</c>,
/// and nothing otherwise.
/// </summary>
[SharedUsing("System.Linq")]
[SharedUsing("System.Console", Static = true)]
public sealed class DefaultUsingsAttribute : UsingBundleAttribute;

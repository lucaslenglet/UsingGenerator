using UsingGenerator;

namespace Demo.Bundles;

[SharedUsing("System.Linq")]
[SharedUsing("System.Console", Static = true)]
public sealed class DemoUsingsAttribute : UsingBundleAttribute;

using System;

namespace UsingGenerator;

/// <summary>
/// Base class for a reusable set of imports. Decorate the derived class with
/// <see cref="SharedUsingAttribute"/> to declare what the bundle carries; the imports stay
/// inert until an assembly applies the derived attribute to itself.
/// </summary>
/// <example>
/// <code>
/// [SharedUsing("System.Linq")]
/// public sealed class MyDefaultUsings : UsingBundleAttribute;
///
/// // in any assembly referencing the one above:
/// [assembly: MyDefaultUsings]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public abstract class UsingBundleAttribute : Attribute;

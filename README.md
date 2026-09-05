# UsingGenerator

Declare a set of `global using` directives once, then apply it to any assembly with a single line.

C# 10 gave us `global using`, but no way to share one set of imports across projects. The usual
workarounds — copying a `GlobalUsings.cs` into every project, or a shared `.props` file with
`<Using Include="..." />` items — both duplicate the list and drift. This generator moves it into a
type that ships in a NuGet package.

## Install

```
dotnet add package lucaslgt.UsingGenerator
```

The package carries both the attributes and the source generator.

## Use

Declare a bundle — anywhere, typically in a shared library:

```csharp
using UsingGenerator;

[SharedUsing("System.Linq")]
[SharedUsing("System.Text.Json")]
[SharedUsing("System.Console", Static = true)]
[SharedUsing("System.Collections.Generic.List<string>", Alias = "StringList")]
public sealed class DefaultUsingsAttribute : UsingBundleAttribute;
```

Apply it to an assembly:

```csharp
[assembly: DefaultUsings]
```

That assembly now compiles as if it contained:

```csharp
global using StringList = System.Collections.Generic.List<string>;
global using System.Linq;
global using System.Text.Json;
global using static System.Console;
```

Any project referencing the library that declares the bundle can apply it — the generator flows
transitively through the package reference, so consumers install nothing extra.

### Nothing happens without opting in

Declaring a bundle imports nothing, **including in the assembly that declares it**. An assembly
gets the imports only by carrying the attribute itself. There is no ambient propagation across a
project reference to surprise you.

### Bundles compose

```csharp
[SharedUsing("System.Text.Json")]
public sealed class WebUsingsAttribute : DefaultUsingsAttribute;
```

`[assembly: WebUsings]` emits `System.Text.Json` plus everything `DefaultUsings` carries. Duplicates
across bundles are emitted once.

## `SharedUsing`

| Member | Effect |
| --- | --- |
| `SharedUsing("N")` | `global using N;` |
| `Static = true` | `global using static N;` |
| `Alias = "X"` | `global using X = N;` |

`Alias` and `Static` are mutually exclusive — there is no such using directive.

## Diagnostics

| Rule | Severity | Raised when |
| --- | --- | --- |
| `UG0001` | Error | `Alias` and `Static` are both set |
| `UG0002` | Warning | The import target is empty; the import is ignored |
| `UG0003` | Warning | `[SharedUsing]` sits on a class that is not a `UsingBundleAttribute` |

## How it works

Attribute constructor and named arguments are constants, so they survive into assembly metadata.
The generator reads the bundle attributes an assembly applies to itself, walks each bundle's base
chain collecting the `[SharedUsing]` attributes declared on it, and emits one `SharedUsings.g.cs`.

Because it reads metadata rather than syntax, a bundle compiled into a referenced DLL resolves
exactly like one declared in the current project. This is also why a bundle cannot carry its
imports in a property or a base constructor call: neither is readable from metadata.

## Repository layout

```
src/       UsingGenerator.Abstractions   the attributes, shipped in lib/
           UsingGenerator.Generator      the IIncrementalGenerator, shipped in analyzers/
samples/   UsingGenerator.Sample.Library declares a bundle, applies nothing
           UsingGenerator.Sample         applies it, plus a local bundle
tests/     UsingGenerator.Tests          drives the generator through a Roslyn driver
           Packaging/                    end-to-end check over a real NuGet package
```

## Build and test

```
dotnet build
dotnet test
./tests/Packaging/run.ps1
```

`run.ps1` packs the library to a local feed, packs a demo library against it, and builds a consumer
that references only that demo library. It is the only check that proves the analyzer reaches a
transitive consumer — the thing most likely to break silently when the packaging changes.

## License

Not yet chosen; the code is all rights reserved until one is added.

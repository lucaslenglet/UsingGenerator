# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build                      # whole solution, 0 warnings is the expected state
dotnet test                       # xunit, drives the generator through a Roslyn driver
dotnet test --filter "Alias_combined_with_static_is_reported_and_skipped"   # one test by name
dotnet run --project samples/UsingGenerator.Sample
./tests/Packaging/run.ps1         # end-to-end over a real NuGet package (see below)
```

`TreatWarningsAsErrors` is on repo-wide, so a warning fails the build.

`git mv` and directory renames fail with `Permission denied` while MSBuild node processes hold
handles on `bin`/`obj`. Run `dotnet build-server shutdown` first.

## What this generates and why the shape is what it is

A `UsingBundleAttribute` subclass carries `[SharedUsing(...)]` attributes. An assembly applying that
subclass to itself gets those imports emitted as `global using` directives in `SharedUsings.g.cs`.

Two constraints drive the whole design:

**Only attribute arguments survive into metadata.** A bundle declared in a referenced DLL has no
syntax to read — no property bodies, no base constructor arguments. That is why the imports hang off
the bundle class as attributes with constant arguments, and why an earlier design (an abstract
`Usings` property overridden in the derived attribute) was abandoned as unimplementable.

**Nothing propagates without opting in.** Only `compilation.Assembly.GetAttributes()` is inspected —
referenced assemblies are never scanned for imports to inherit. Declaring a bundle imports nothing,
including in the declaring assembly. Changing this would reverse a deliberate decision; do not
"fix" it as an oversight.

## Incremental generator invariants

`Collect` hangs off `CompilationProvider`, which Roslyn invalidates on every keystroke. Everything
downstream depends on the collected value comparing equal across runs, so:

- **No Roslyn object may enter a pipeline value.** `ISymbol`, `Compilation`, `SyntaxNode` and
  `Location` all pin the compilation in memory and compare by reference or by syntax-tree identity,
  which re-runs the generator on every edit. `EquatableArray<T>` and `LocationInfo` exist for this;
  `DiagnosticInfo` exists because `Diagnostic` cannot be pipelined.
- `ForAttributeWithMetadataName` transforms must reduce to plain data **inside** the lambda.
- `IncrementalCachingTests` guards this. `A_reported_diagnostic_does_not_break_caching` uses
  `ReplaceSyntaxTree`, not `AddSyntaxTrees` — adding a file leaves the original tree instance intact
  and the test would pass even when broken.

Attribute identity is matched by **name plus namespace**, not `SymbolEqualityComparer`. This is
deliberate: it keeps working when the attributes assembly resolves to an error symbol or is present
in two versions. `ToDisplayString()` is avoided — it allocates per symbol visit, on every keystroke.

## netstandard2.0 constraints (generator project only)

The generator must target netstandard2.0 to be loadable by Roslyn. Consequences:

- List patterns need `System.Index`, which is absent — hence the length check in `ReadSpec`.
- `record struct` init setters need `Polyfills/IsExternalInit.cs`.
- `AnalyzerReleases.{Shipped,Unshipped}.md` are required by RS2008 whenever a `DiagnosticDescriptor`
  is added; the separator row format is strict and RS2007 rejects a malformed one.

## Packaging

`src/UsingGenerator.Abstractions` is the packaging project (`PackageId` is `lucaslgt.UsingGenerator`; the namespaces stay `UsingGenerator`): its own
assembly goes to `lib/`, and `PackGeneratorAsAnalyzer` packs the generator to `analyzers/dotnet/cs`
from the `@(Analyzer)` item that its `ProjectReference` populates.

A library that declares a bundle needs **no** special `PrivateAssets` for its consumers to get the
generator — measured, the analyzer flows transitively at the default setting. `PrivateAssets="all"`
does break consumers: it also hides the attributes (CS0012).

`tests/Packaging/run.ps1` is what proves this. It packs to a local feed under `.artifacts/`, packs a
demo library against it, and builds a consumer referencing only that demo library. It wipes the feed,
an isolated package cache and the demo `obj`/`bin` on each run, because a repacked package at an
unchanged version would otherwise be served from cache and the test would pass without testing
anything.

`tests/Packaging/Demo.App/` is force-tracked in `.gitignore`: the stock template's `*.app` rule
(macOS bundles) matches the directory name and silently excluded it.

## MSBuild layout

- `Directory.Build.props` — shared compiler settings plus `UsingGeneratorVersion` /
  `DemoBundlesVersion`, the single source for package versions. `run.ps1` reads the former via
  `dotnet msbuild -getProperty`.
- `Directory.Build.targets` — `EmitCompilerGeneratedFiles` lives here, **not** in `.props`:
  `BaseIntermediateOutputPath` is unset that early, and generated files would land in the project
  root where the default globs compile them a second time.
- `Directory.Packages.props` — central package management. The Roslyn version is shared by the
  generator and the tests on purpose; bumping one alone makes the tests exercise a different API
  surface than the one shipped.

## Release

Pushing a `v*` tag runs `.github/workflows/release.yml`: the tag is the only source of the published
version (`v1.2.3` -> `1.2.3`), so nothing in the repo is bumped to release. The workflow packs at
that version through `run.ps1`, and pushes **that same validated artifact** to nuget.org — never a
separately packed one.

Publishing uses nuget.org trusted publishing (OIDC), not a stored API key. The job needs
`id-token: write`, and the `NuGet/login@v1` step must stay immediately before the push: it trades
one OIDC token for one API key valid for a single hour. The only repository secret is `NUGET_USER`
(the nuget.org profile name). The matching policy lives on nuget.org and names the repository owner,
the repository, and the workflow file name alone — `release.yml`, without its directory.

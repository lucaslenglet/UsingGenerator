# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- MIT-0 license, now declared in the package metadata.
- Release notes on nuget.org and on the GitHub release, taken from this file.

## [1.0.0] - 2026-09-05

### Added

- `UsingBundleAttribute` and `[SharedUsing]`: declare a set of `global using` directives once
  (plain, `Static` or `Alias`), then apply it to any assembly with a single attribute.
- Bundles compose through inheritance; duplicates are emitted once.
- Diagnostics UG0001 to UG0003.

[Unreleased]: https://github.com/lucaslenglet/UsingGenerator/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/lucaslenglet/UsingGenerator/releases/tag/v1.0.0

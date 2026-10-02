# Changelog

All notable PureSharp release-line changes are recorded here. Diagnostic compatibility is governed by [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md).

## [1.0.0] - 2026-10-02

### Added

- Stable public contract for `RT0001`-`RT0003`, `LVP0001`-`LVP0003`, and `FIF0001`.
- Clean NuGet consumer verification, including analyzer discovery and `.editorconfig` suppression.
- Compatibility matrix and representative analyzer performance gate.
- Complete v1 documentation, rule examples, migration guidance, and contribution policy.

### Changed

- Hardened immutable-local handling for `ref`/`out`, deconstruction reassignment, captured locals, `foreach`, and pattern boundaries.
- Hardened FluentIf termination analysis for nested, lambda-contained, generic, parenthesized, and unrelated-member-call chains while allowing normal calls after a valid `Else(...)` result.
- Defined `[PureMethod]` call behavior against the statically resolved symbol and documented interface/virtual/overload/external dependency boundaries.

### Compatibility notes

- The seven existing diagnostic IDs remain the v1 public IDs.
- v1 intentionally widens detection in several LVP/FIF edge cases compared with 0.1.x; see [`docs/MIGRATION-1.0.md`](docs/MIGRATION-1.0.md).
- Known RT false-negative boundaries are documented rather than hidden; see [`docs/PURITY-SEMANTICS.md`](docs/PURITY-SEMANTICS.md).

## [0.1.6]

- Centralized the package version in `Directory.Build.props` as the repository version SSOT.
- Removed duplicate project-level version metadata and validated tag/package release automation.

## [0.1.3]

- Established the RT, LVP, and FIF diagnostic families that are stabilized by the v1 contract.

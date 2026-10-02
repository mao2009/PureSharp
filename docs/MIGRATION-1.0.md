# Migrating PureSharp 0.x to 1.0

PureSharp 1.0 stabilizes the diagnostic contract and documents previously implicit analyzer boundaries. The package ID remains `loach.PureSharp` and the seven existing diagnostic IDs remain the v1 public IDs.

## What remains stable

The v1 contract keeps these IDs:

- `RT0001`, `RT0002`, `RT0003`
- `LVP0001`, `LVP0002`, `LVP0003`
- `FIF0001`

Per-ID severity configuration continues to use Roslyn's standard `.editorconfig` syntax:

```editorconfig
[*.cs]
dotnet_diagnostic.RT0001.severity = error
dotnet_diagnostic.LVP0003.severity = warning
```

There is no PureSharp-specific rule-selection format to migrate.

## Behavior to review before upgrading

### LVP mutation coverage is stricter

v1 locks additional immutable-local mutation paths that older 0.x builds could miss. In particular, underscore-prefixed locals are protected when they are passed by `ref` or `out` and when they are targets of deconstruction reassignment. `in` arguments remain allowed.

If 0.x code relied on mutating an underscore-prefixed local through one of those paths, rename the local as mutable or remove the mutation rather than suppressing `LVP0001` globally.

### FluentIf termination analysis is more precise

v1 follows chains from the resolved `PureSharp.Core.Fluent.If` entry point. An unrelated call such as `Fluent.If(...).ToString()` does not count as a terminator; the condition chain must reach the matching `Else(...)`. Ordinary calls after a valid result `Else(...)` are allowed.

Nested chains and chains in lambdas are checked independently.

### Purity boundaries are explicit, not magically complete

A clean RT analysis is a guarantee only for the documented v1 subset. Important known false-negative boundaries include instance-state mutation, some non-deterministic members on known-pure types, constructor bodies, user-defined property getters, delegate targets, and mutation through references. Read [`PURITY-SEMANTICS.md`](PURITY-SEMANTICS.md) and [`CALL-CONTRACT.md`](CALL-CONTRACT.md) before treating `[PureMethod]` as a proof of whole-program purity.

## Recommended migration procedure

1. Upgrade the package reference to `loach.PureSharp` 1.0.0 when published.
2. Run a clean build with no new suppressions.
3. Fix newly exposed `LVP0001` or `FIF0001` violations according to the v1 contract.
4. Review existing `.editorconfig` entries by diagnostic ID. Message text is not a compatibility surface.
5. Read the known limitations and support matrix.
6. Pin the package version in production projects.
7. Run your normal CI with warnings visible; do not hide `LVP0003` unless the naming convention is intentionally not being adopted.

## Breaking-change policy from v1 onward

The normative policy is in [`DIAGNOSTICS.md`](DIAGNOSTICS.md). In summary:

- Changing a diagnostic ID is never done in place; the old ID is retired and a new one allocated.
- Changing category, raising default severity, widening detection, or removing a diagnostic is a major-version change.
- Narrowing detection or lowering severity may occur in a minor version when documented.
- Title/message/description wording and translations are not compatibility surface.
- New default-error diagnostics are treated as breaking because they can turn a previously successful build into a failure.

## If migration must be staged

Use `.editorconfig` to change one rule at a time instead of forking the analyzer configuration model. For example:

```editorconfig
[*.cs]
dotnet_diagnostic.LVP0003.severity = none
```

Then re-enable the rule after the codebase is ready. The release consumer test verifies that standard Roslyn suppression works from the packed NuGet package.

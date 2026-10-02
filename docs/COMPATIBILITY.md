# PureSharp v1 compatibility and performance policy

This document defines the compatibility matrix and performance gate used for the PureSharp v1 release line.

## Compatibility matrix

| Area | v1 support policy | CI / release evidence |
|---|---|---|
| Runtime/Core target | `netstandard2.0` | `PureSharp.Core.csproj` targets `netstandard2.0` |
| Consumer target frameworks | Any TFM that can reference `netstandard2.0`; the release gate validates `net10.0` explicitly | `scripts/verify-consumer-package.sh` |
| Language | C# only | Analyzer registration uses `LanguageNames.CSharp` |
| Minimum C# baseline | C# 10-era syntax and semantics | Analyzer assembly is built against Roslyn 4.0.1 |
| Newer C# versions | Supported when hosted by a compatible Roslyn 4.x+ compiler; syntax-specific behavior is covered incrementally by analyzer tests | Current CI runs on .NET SDK 10.0.103 and analyzer tests use Roslyn 4.12 packages |
| Minimum Roslyn host | Roslyn 4.0 | `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.0.1 is the analyzer compile-time baseline |
| Validated Roslyn test API | Roslyn 4.12 | Test project package references |
| CLI build | Supported through `dotnet build` on a compatible SDK | GitHub Actions build/test/package/consumer gates |
| IDE hosts | Supported when the IDE provides a compatible Roslyn 4.x+ analyzer host | IDE product/version combinations are not individually certified in v1 |

## Unsupported / not guaranteed combinations

- Roslyn versions older than 4.0.
- Non-C# languages.
- Consumer TFMs that cannot reference `netstandard2.0`.
- IDE-specific behavior that differs from the underlying Roslyn analyzer host.
- Preview language constructs for which Roslyn changes the operation/symbol model incompatibly before release.

The CLI build is the authoritative compatibility gate for v1. IDE diagnostics should match the Roslyn compiler host, but PureSharp does not promise a separate compatibility matrix for every Visual Studio, Rider, or VS Code release.

## Performance representative workload

`scripts/verify-analyzer-performance.sh` creates a clean temporary consumer project from the locally packed `loach.PureSharp` package. It generates **250 C# source files** by default. Each source file contains a `[PureMethod]` method and several underscore-prefixed immutable locals, so the RT and LVP analyzer families execute on realistic syntax and semantic data without intentionally producing diagnostics.

The script then:

1. restores the clean consumer from the local `.nupkg` plus NuGet.org,
2. performs one warm Release build,
3. performs a measured `Release --no-incremental` build,
4. fails if the measured build exceeds the configured threshold.

The measurement intentionally includes compiler and analyzer execution together. It is a release regression gate, not a microbenchmark of one analyzer callback. This makes the gate closer to consumer-observed build cost and keeps it reproducible with the normal .NET CLI.

## Threshold

The hard v1 CI threshold is **15,000 ms** for the default 250-source representative workload on the GitHub-hosted Ubuntu runner.

- Environment override: `PURESHARP_ANALYZER_PERF_MAX_MS`
- Workload-size override: `PURESHARP_ANALYZER_PERF_SOURCE_COUNT`
- Any result above the hard threshold fails CI and blocks release/merge.
- A result that grows to more than **2x the recorded baseline** should be investigated even if it remains under the hard threshold.

The 15-second gate is deliberately conservative to avoid flaky failures caused by shared-runner variance while still detecting order-of-magnitude analyzer regressions.

## Baseline recording

The CI script prints a line in this form:

```text
PureSharp analyzer performance sample: package=<version> sources=250 elapsed_ms=<value> threshold_ms=15000
```

For v1 release preparation, record the latest successful main/RC measurement here and compare subsequent release candidates against it.

- Initial v1 baseline: **pending first successful performance-gate CI run**
- Baseline environment: GitHub-hosted Ubuntu, .NET SDK 10.0.103, Release build, 250 generated source files

## Regression policy

A compatibility or performance regression is release-blocking when any of the following is true:

- the clean package consumer no longer restores/builds,
- an analyzer cannot load in the supported Roslyn baseline,
- representative analyzer tests fail under the validated Roslyn test packages,
- the performance gate exceeds 15,000 ms,
- a release-candidate measurement exceeds 2x the recorded baseline without an accepted explanation.

Changes to the supported Roslyn minimum, language baseline, runtime target, or hard performance threshold must be documented as release-policy changes and reviewed together with the v1 compatibility contract.

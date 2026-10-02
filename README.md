# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

[![CI & NuGet Upload](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml/badge.svg)](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml) [![NuGet](https://img.shields.io/nuget/v/loach.PureSharp.svg)](https://www.nuget.org/packages/loach.PureSharp) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![X (Twitter) Follow](https://img.shields.io/twitter/follow/loach_mao)](https://x.com/loach_mao)

**PureSharp** brings functional-programming safety to C# through Roslyn analyzers and a small runtime API. It focuses on three contracts: referential transparency, immutable local variables, and safe FluentIf termination.

## Install

```bash
dotnet add package loach.PureSharp --version 1.0.0
```

The package contains both `PureSharp.Core` and the analyzer assembly. No PureSharp-specific configuration file is required; diagnostics use standard Roslyn `.editorconfig` settings.

For a clean-project walkthrough, use [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md).

## Core concepts

1. **Purity enforcement:** declare side-effect-free logic with `[PureMethod]` and verify the documented v1 subset mechanically.
2. **Immutable locals:** opt into non-reassignable local variables by starting their names with `_`.
3. **Safe control flow:** use `Fluent.If` chains that must terminate with `.Else(...)`.

## Key features

### `[PureMethod]`

```csharp
using PureSharp.Core;

public static class Calculator
{
    [PureMethod]
    public static int Add(int a, int b) => a + b;
}
```

Inside `[PureMethod]`, PureSharp reports static mutable-state access, non-pure method calls, and I/O according to the v1 contract. PureSharp does **not** claim to prove whole-program purity; see [`docs/PURITY-SEMANTICS.md`](docs/PURITY-SEMANTICS.md) and [`docs/CALL-CONTRACT.md`](docs/CALL-CONTRACT.md) for the exact guarantee boundary.

### Immutable local variables

```csharp
var _value = Calculate();
// _value = 10; // LVP0001
```

Underscore-prefixed locals must be initialized at declaration and cannot later be mutated through covered assignment paths. `LVP0003` suggests the convention for effectively immutable locals. See [`docs/LVP.md`](docs/LVP.md).

### FluentIf

```csharp
var _status = Fluent.If(score >= 80, () => 1)
    .ElseIf(score >= 60, () => 2)
    .Else(0);
```

A chain that starts with `Fluent.If(...)` must reach the matching `.Else(...)`; otherwise `FIF0001` is reported. Nested chains, lambdas, generic inference, branch short-circuiting, and exception behavior are specified in [`docs/FLUENT_IF.md`](docs/FLUENT_IF.md).

## Supported diagnostics

| Diagnostic ID | Category | Title | Default severity |
|---|---|---|---|
| **RT0001** | Purity | Static field access in `[PureMethod]` | Error |
| **RT0002** | Purity | Non-pure method call in `[PureMethod]` | Error |
| **RT0003** | Purity | I/O operation in `[PureMethod]` | Error |
| **LVP0001** | Purity | Reassignment to immutable local variable prohibited | Error |
| **LVP0002** | Purity | Mandatory initialization of immutable local variable | Error |
| **LVP0003** | Naming | Suggestion to apply naming convention for immutable local variable | Warning |
| **FIF0001** | FluentIf | FluentIf chain termination check | Error |

The authoritative contract is [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md). Minimal examples for every rule are in [`docs/RULE-EXAMPLES.md`](docs/RULE-EXAMPLES.md).

## Diagnostic configuration

Create or update `.editorconfig` in the consumer project:

```editorconfig
root = true

[*.cs]
dotnet_diagnostic.RT0001.severity = error
dotnet_diagnostic.RT0002.severity = error
dotnet_diagnostic.RT0003.severity = error
dotnet_diagnostic.LVP0001.severity = error
dotnet_diagnostic.LVP0002.severity = error
dotnet_diagnostic.LVP0003.severity = warning
dotnet_diagnostic.FIF0001.severity = error
```

Use normal Roslyn values (`none`, `silent`, `suggestion`, `warning`, `error`). For staged adoption, override individual IDs instead of enabling/disabling whole rule families through a custom mechanism.

Example:

```editorconfig
[*.cs]
dotnet_diagnostic.LVP0003.severity = none
```

## Compatibility and known limitations

PureSharp.Core and the analyzer target `netstandard2.0`; the supported compiler/Roslyn policy and measured performance gate are documented in [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md).

The v1 purity analyzer intentionally has documented boundaries. A clean analysis means that no violation of the **supported subset** was found, not that the method has been formally proven referentially transparent. Read the purity and call-contract documents before treating analyzer silence as a stronger guarantee.

## Upgrading from 0.x

See [`docs/MIGRATION-1.0.md`](docs/MIGRATION-1.0.md). The seven diagnostic IDs remain the v1 public IDs, while v1 hardens several edge cases such as immutable-local `ref`/`out` mutation, deconstruction reassignment, and FluentIf termination analysis.

## Documentation

- [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md) — clean consumer Quick Start.
- [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md) — diagnostic SSOT and compatibility policy.
- [`docs/RULE-EXAMPLES.md`](docs/RULE-EXAMPLES.md) — all seven diagnostics with examples.
- [`docs/PURITY-SEMANTICS.md`](docs/PURITY-SEMANTICS.md) — RT semantics and known limitations.
- [`docs/CALL-CONTRACT.md`](docs/CALL-CONTRACT.md) — `[PureMethod]` call rules.
- [`docs/LVP.md`](docs/LVP.md) — immutable-local contract.
- [`docs/FLUENT_IF.md`](docs/FLUENT_IF.md) — FluentIf contract.
- [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md) — supported versions and performance policy.
- [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md) — documentation/translation SSOT policy.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — contribution workflow.

## Project structure

- **PureSharp.Core** — runtime API and Roslyn analyzers, packaged from `netstandard2.0`.
- **PureSharp.Analyzers.Tests** — analyzer/runtime contract tests (xUnit; current CI uses .NET 10).

## Motivation

C# is powerful, but unintended side effects and variable reuse can make large codebases harder to reason about. PureSharp deliberately uses constraints to make those assumptions visible to the compiler and CI.

## License

PureSharp is released under the MIT License.

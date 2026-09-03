# PureSharp Diagnostic Catalog (SSOT)

**This document is the single source of truth for PureSharp's diagnostic contract.**

Where this catalog, the analyzer source, and the README disagree, this catalog defines
the intent and the other two must be corrected to match it.

- Applies to: PureSharp v1.0.0
- Diagnostics are configured through Roslyn's standard `.editorconfig` mechanism
  (`dotnet_diagnostic.<ID>.severity`). See the README for configuration examples.

## Catalog

Every diagnostic PureSharp can report is listed here. There are no unlisted diagnostics.

### RT — Referential Transparency

Emitted by `ReferentialTransparencyAnalyzer`. These apply **only inside methods marked
with `[PureMethod]`**.

The purity model these diagnostics approximate — and, importantly, the cases v1.0 does
**not** detect — is specified in [`PURITY-SEMANTICS.md`](PURITY-SEMANTICS.md).

| Field | Value |
|---|---|
| **ID** | `RT0001` |
| **Category** | `Purity` |
| **Default severity** | `Error` |
| **Enabled by default** | Yes |
| **Title** | Static field access in `[PureMethod]` |
| **Message** | Cannot access static, non-readonly field `'{1}'` within `[PureMethod]` method `'{0}'` |
| **Description** | Referentially transparent methods must not access static mutable state. |
| **Target** | Field reference where the field is `static` and neither `readonly` nor `const` |

| Field | Value |
|---|---|
| **ID** | `RT0002` |
| **Category** | `Purity` |
| **Default severity** | `Error` |
| **Enabled by default** | Yes |
| **Title** | Non-pure method call in `[PureMethod]` |
| **Message** | Cannot call non-pure method `'{1}'` within `[PureMethod]` method `'{0}'` |
| **Description** | Referentially transparent methods can only call methods marked with the `[PureMethod]` attribute or known pure types. |
| **Target** | Invocation of a method that is neither `[PureMethod]`-marked nor on a known-pure type |

| Field | Value |
|---|---|
| **ID** | `RT0003` |
| **Category** | `Purity` |
| **Default severity** | `Error` |
| **Enabled by default** | Yes |
| **Title** | I/O operation in `[PureMethod]` |
| **Message** | Cannot perform I/O operations within `[PureMethod]` method `'{0}'` |
| **Description** | Referentially transparent methods must not perform I/O operations such as logging, file access, or network access. |
| **Target** | Invocation or property access on an I/O type |

### LVP — Local Variable Purity

`LVP0001` and `LVP0002` are emitted by `LocalVariablePurityAnalyzer`;
`LVP0003` is emitted by `ImmutableNamingSuggestionAnalyzer`.

An **immutable local** is a local variable whose name begins with `_`. The single
underscore `_` is the discard symbol and is never treated as an immutable local.

| Field | Value |
|---|---|
| **ID** | `LVP0001` |
| **Category** | `Purity` |
| **Default severity** | `Error` |
| **Enabled by default** | Yes |
| **Title** | Reassignment to immutable local variable prohibited |
| **Message** | Cannot reassign a value to immutable local variable `'{0}'` |
| **Description** | Local variables starting with an underscore (`_`) are treated as immutable, and reassignment after declaration is prohibited. |
| **Target** | Assignment, compound assignment, or increment/decrement targeting an immutable local |

| Field | Value |
|---|---|
| **ID** | `LVP0002` |
| **Category** | `Purity` |
| **Default severity** | `Error` |
| **Enabled by default** | Yes |
| **Title** | Mandatory initialization of immutable local variable |
| **Message** | Immutable local variable `'{0}'` must be initialized at the time of declaration |
| **Description** | Local variables starting with an underscore (`_`) are treated as immutable and must be assigned an initial value at declaration. |
| **Target** | Declaration of an immutable local with no initializer |

| Field | Value |
|---|---|
| **ID** | `LVP0003` |
| **Category** | `Naming` |
| **Default severity** | `Warning` |
| **Enabled by default** | Yes |
| **Title** | Suggestion to apply naming convention for immutable local variable |
| **Message** | Local variable `'{0}'` is effectively immutable. Consider starting its name with an underscore `'_'`. |
| **Description** | Local variables that are never reassigned can have their immutability explicitly shown by starting the name with an underscore. |
| **Target** | A local variable that is never reassigned but whose name does not begin with `_` |

### FIF — FluentIf

Emitted by `FluentIfAnalyzer`.

| Field | Value |
|---|---|
| **ID** | `FIF0001` |
| **Category** | `FluentIf` |
| **Default severity** | `Error` |
| **Enabled by default** | Yes |
| **Title** | FluentIf chain termination check |
| **Message** | FluentIf chain must be terminated with `'.Else(...)'` |
| **Description** | Method chains starting with `Fluent.If()` must always end with `.Else()`. |
| **Target** | A `Fluent.If()` chain not terminated by `.Else(...)` |

## Summary table

| ID | Category | Default severity | Analyzer | v1.0 public contract |
|---|---|---|---|---|
| `RT0001` | Purity | Error | `ReferentialTransparencyAnalyzer` | Yes |
| `RT0002` | Purity | Error | `ReferentialTransparencyAnalyzer` | Yes |
| `RT0003` | Purity | Error | `ReferentialTransparencyAnalyzer` | Yes |
| `LVP0001` | Purity | Error | `LocalVariablePurityAnalyzer` | Yes |
| `LVP0002` | Purity | Error | `LocalVariablePurityAnalyzer` | Yes |
| `LVP0003` | Naming | Warning | `ImmutableNamingSuggestionAnalyzer` | Yes |
| `FIF0001` | FluentIf | Error | `FluentIfAnalyzer` | Yes |

## v1.0 public contract

All seven diagnostics above are **part of the v1.0.0 public contract**. For each of them,
the following are contract surface that consumers may depend on:

- The **ID** string
- The **category**
- The **default severity**
- The **fact that the diagnostic exists and is enabled by default**

The following are **not** contract surface and may change in any release:

- The exact wording of the title, message, and description (including translations)
- The internal analyzer class that emits the diagnostic
- The precise implementation strategy used to detect the condition

Message wording is deliberately excluded so that clarity improvements and localization
work are not breaking changes. Consumers must match on the diagnostic **ID**, never on
message text.

## ID naming convention

```
<PREFIX><NNNN>
```

- **PREFIX** — a short uppercase prefix identifying the rule family:
  | Prefix | Family |
  |---|---|
  | `RT` | Referential Transparency — purity of `[PureMethod]` bodies |
  | `LVP` | Local Variable Purity — immutable local variable rules |
  | `FIF` | FluentIf — `Fluent.If()` chain rules |
- **NNNN** — a zero-padded four-digit number, allocated sequentially within the family
  starting at `0001`.

Rules:

- A prefix identifies the **rule family**, not the analyzer class. `LVP0003` is emitted
  by `ImmutableNamingSuggestionAnalyzer` but belongs to the LVP family because it is a
  local-variable purity concern.
- Numbers are **never reused**, including after a diagnostic is removed.
- A new rule family requires a new prefix and an entry in the table above.

## Category policy

| Category | Meaning |
|---|---|
| `Purity` | The code violates a purity or immutability guarantee. Correctness-affecting. |
| `Naming` | The code is correct, but a naming convention would express intent better. Advisory. |
| `FluentIf` | The code misuses the FluentIf API surface. Correctness-affecting. |

Guidance:

- `Purity` and `FluentIf` diagnostics default to `Error` — they indicate the code does
  not satisfy a guarantee PureSharp is asked to enforce.
- `Naming` diagnostics default to `Warning` or lower — they are suggestions and must
  never block a build by default.
- A diagnostic's category must not change once the diagnostic is part of a released
  public contract; see the compatibility rules below.

## Compatibility rules

### Adding a new diagnostic

Adding a diagnostic that is **enabled by default** can fail a build that previously
succeeded, so it is treated as a breaking change.

- Allocate the next unused number in the family; never reuse a retired number.
- Add it to this catalog and to the README table in the same change.
- Add positive and negative tests in the same change.
- Prefer introducing a new default-`Error` diagnostic only in a **major** release.
  In a minor release, introduce it as `Warning` or `isEnabledByDefault: false` first.

### Changing an existing diagnostic

| Change | Allowed in | Notes |
|---|---|---|
| Title / message / description wording | Any release | Not contract surface. Consumers must not match on text. |
| Translation updates | Any release | Same as above. |
| Widening detection (more code now reported) | Major release | Can break a previously passing build. |
| Narrowing detection (less code now reported) | Minor release | Does not break builds; may weaken a guarantee, so document it. |
| Raising default severity | Major release | Can break a previously passing build. |
| Lowering default severity | Minor release | Document the reduced guarantee. |
| Changing category | Major release | Category is contract surface; it appears in consumer tooling. |
| Changing ID | Never | Retire the old ID and allocate a new one instead. |

### Removing a diagnostic

- Removal is a **major**-release change.
- The retired ID stays listed in this catalog under a "Retired" heading with the version
  in which it was removed, and its number is never reused.
- Consumers suppressing the retired ID in `.editorconfig` must not start failing; an
  unknown `dotnet_diagnostic` entry is ignored by Roslyn, so no migration is required.

### Change checklist

Any change touching a `DiagnosticDescriptor` must update, in the same commit:

- [ ] The descriptor in the analyzer source
- [ ] This catalog
- [ ] The README diagnostic table (and translated READMEs where they carry the table)
- [ ] Tests covering the new or changed behaviour

## Retired diagnostics

None. No diagnostic has been removed from PureSharp.

## Consistency verification

The three representations of the diagnostic contract must agree:

1. `DiagnosticDescriptor` definitions in `src/PureSharp.Core/*Analyzer.cs`
2. Resource strings in `src/PureSharp.Core/Resources/DiagnosticResources.resx`
   (and per-language `.resx` files)
3. This catalog and the README table

Each descriptor must reference the resource keys belonging to **its own ID** —
`<ID>_Title`, `<ID>_MessageFormat`, and `<ID>_Description`. A descriptor referencing
another diagnostic's resource key is a defect: it surfaces the wrong explanatory text to
consumers even though the ID, category, and severity look correct.

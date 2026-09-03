# PureSharp `[PureMethod]` Call Contract

**How `[PureMethod]` behaves across call boundaries, and how far v1.0 analyses.**

This document specifies the call contract enforced by `RT0002`. For the purity model
itself see [`PURITY-SEMANTICS.md`](PURITY-SEMANTICS.md); for the diagnostic contract see
[`DIAGNOSTICS.md`](DIAGNOSTICS.md).

Every behaviour below was **observed** by running the analyzer and is locked by
`CallContractTests`.

## Design goal

v1.0 deliberately does **not** attempt unrestricted whole-program analysis. The goal is a
contract that is **decidable, reproducible, and fast** inside a Roslyn analyzer:

- decidable from the current compilation alone,
- independent of build order and of which assemblies happen to have source available,
- linear in the size of the analysed code.

Whole-program purity inference would fail all three.

## The contract

> `[PureMethod]` is a **declared contract on a symbol**, not an inferred property of an
> implementation.

From a `[PureMethod]` method you may call:

1. a method whose **statically resolved symbol** carries `[PureMethod]`, or
2. a method on a **known-pure type**.

Everything else is `RT0002`. PureSharp never opens the callee's body to decide whether it
happens to be pure — an unmarked method is treated as impure, always.

This is a **fail-closed** rule: it can over-report, never under-report, for the calls it
covers.

## Resolution rules

The contract is checked against the symbol the compiler resolves the call to. This has
consequences worth stating explicitly, because they are easy to get wrong.

| Call shape | Where `[PureMethod]` must be | Behaviour |
|---|---|---|
| Interface method | On the **interface declaration** | Attribute on the implementing class does **not** satisfy a call made through the interface — the call resolves to the interface member |
| `virtual` method called through the base type | On the **base declaration** | Attribute on the `override` alone does **not** satisfy it |
| `override` called through the derived type | On the resolved (derived) symbol | Normal resolution applies |
| Overloads | On the **specific overload** selected | Each overload is judged independently |
| Extension method | On the extension method itself | Resolves to the static method |
| Static method | On the method | Normal resolution applies |
| Recursion, mutual recursion | On **every** participant | One unmarked participant produces `RT0002` at that call site |
| Generic method | On the generic definition | Type arguments do not affect the decision |

### Consequence: virtual dispatch is not sound

Marking a base `virtual` method `[PureMethod]` permits every call made through the base
type, but an override may be impure. PureSharp does not verify that overrides honour the
base contract.

```csharp
public class Base { [PureMethod] public virtual int M() => 1; }
public class Derived : Base { public override int M() { Console.WriteLine(); return 2; } }
```

A call to `Base.M()` from a pure method is accepted, and `Derived.M()` is not checked
against the base's contract. **Marking a virtual or interface member `[PureMethod]` is an
assertion the author is responsible for upholding in every override.**

Verifying that overrides preserve the contract is a candidate for a future diagnostic
(see Open decisions).

## Interprocedural scope

**In scope for v1.0:** exactly one level — the call site, judged against the resolved
callee's declared attribute.

**Not in scope for v1.0:**

- Reading the callee's body to infer purity
- Propagating purity transitively through unmarked methods
- Verifying that overrides honour a base `[PureMethod]` contract
- Whole-program or cross-compilation analysis
- Purity of delegate targets (see `PURITY-SEMANTICS.md`)

The analysis performs **no cross-method traversal**. Each call site is decided from the
resolved symbol's attributes and containing type name alone, which is what keeps the cost
linear.

### Not covered by the call contract

Two call-like constructs are not analysed as invocations at all:

| Construct | Status |
|---|---|
| User-defined property getter | **Not checked.** Property references are inspected only for I/O types, so a getter running arbitrary code is invisible. |
| Constructor (`new T()`) | **Not checked.** Object creation is not an invocation operation here, so a constructor body doing anything at all is invisible. |

Both are false negatives, locked by `FALSE_NEGATIVE_*` tests in `CallContractTests`.

## External dependencies

**Decision: external assemblies are handled by the same fail-closed rule, with no special
casing.**

A method from an assembly PureSharp does not control cannot carry `[PureMethod]`, so:

- if its containing type is on the **known-pure list**, the call is allowed;
- otherwise the call is `RT0002`.

```csharp
[PureMethod] public object Run() => Math.Abs(-1);                          // allowed
[PureMethod] public object Run() => new StringBuilder().ToString();        // RT0002
```

The alternatives were rejected:

| Alternative | Why rejected |
|---|---|
| Read external metadata/IL to infer purity | Not decidable in an analyzer; depends on whether reference assemblies or full IL are available; not reproducible across build environments |
| Trust external methods by default | Fails open — silently permits arbitrary I/O |
| Honour a `[Pure]`-style attribute from other libraries | `System.Diagnostics.Contracts.PureAttribute` has weaker, inconsistently applied semantics; adopting it would import an unverified guarantee |

The consequence is accepted: the known-pure list is an **allowlist**, and any pure BCL
type absent from it over-reports. Growing that list is a non-breaking change (it narrows
detection), so gaps can be fixed in minor releases.

## Performance

Measured on the analyzer as shipped, on synthetic compilations of `[PureMethod]` methods
each containing a pure call, an arithmetic call, and a local assignment.

| Methods analysed | Analyzer time | Per method |
|---|---|---|
| 100 | 186 ms | 1.86 ms |
| 500 | 349 ms | 0.70 ms |
| 2 000 | 844 ms | 0.42 ms |

Observations:

- Cost grows **linearly** with the number of analysed operations. Per-method cost falls as
  fixed startup cost is amortised; the 100-method figure is dominated by JIT warm-up.
- Analyzer time is of the same order as the baseline compilation of the same source
  (roughly 0.8–1.1×), which is the expected range for an operation-walking analyzer.
- There is no super-linear behaviour, because no call graph is built and no callee body is
  visited. This is the practical payoff of the one-level contract.

**Method.** Build a `CSharpCompilation` from generated source, wrap it with
`WithAnalyzers`, and time `GetAnalyzerDiagnosticsAsync()`. `CallContractTests`
contains a reproducible guard at 2 000 methods; its bound is deliberately generous
(60 s) so it detects catastrophic regressions without becoming flaky on shared CI.

Absolute numbers are machine-dependent and are recorded for **shape**, not as a
threshold to enforce.

## Open decisions

Each would be a breaking change under `DIAGNOSTICS.md` (widening detection → major
release) and needs a product decision.

1. **Override contract verification** — report when an `override` of a `[PureMethod]`
   member is not itself pure. Would close the virtual-dispatch soundness gap.
2. **Attribute inheritance** — should an `override` inherit the base's `[PureMethod]`, so
   marking only the override is unnecessary and calls through the derived type behave
   consistently?
3. **Property getters** — treat a user-defined getter as an invocation for contract
   purposes.
4. **Constructors** — bring object creation under the call contract.
5. **Local functions** — let a local function inherit the enclosing method's contract, so
   its invocation stops producing `RT0002` (see `PURITY-SEMANTICS.md`, gap 7).

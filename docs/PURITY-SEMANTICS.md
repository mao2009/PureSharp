# PureSharp Purity Semantics (RT)

**What `[PureMethod]` means, what PureSharp v1.0 guarantees, and what it does not.**

This document defines the judgement principle for the RT (Referential Transparency)
diagnostics. For the diagnostic IDs, categories, severities, and compatibility rules see
[`DIAGNOSTICS.md`](DIAGNOSTICS.md), which is the SSOT for the diagnostic contract.

Every behaviour recorded here was **observed** by running the analyzer, not inferred from
reading the source. The observations are locked by `PuritySemanticsTests`, so this
document and the implementation cannot drift apart silently.

## Judgement principle

A method marked `[PureMethod]` is expected to be **referentially transparent**: replacing
a call to it with its result must not change the meaning of the program.

Two properties follow, and they are the standard against which every rule below is
judged:

1. **No observable state change.** The method must not mutate state that anything outside
   the method can observe.
2. **Determinism.** The same arguments must always produce the same result.

PureSharp deliberately does **not** define purity as "does not call anything on a
blocklist". A blocklist cannot express why something is impure, and it silently permits
every impure API nobody thought to list. The rules below approximate the two properties
above; where the approximation is incomplete, that gap is recorded as a false negative
rather than presented as a guarantee.

## What v1.0 guarantees

These are detected. Each row is locked by a test in `PuritySemanticsTests`.

| Behaviour | Diagnostic |
|---|---|
| Reading a `static`, non-`readonly`, non-`const` field | `RT0001` |
| Writing a `static`, non-`readonly`, non-`const` field | `RT0001` |
| Calling a method on an I/O type (`System.Console`, `System.IO.*`, `System.Net.*`, `NLog.*`, `Microsoft.Extensions.Logging.*`) | `RT0003` |
| Reading a property on an I/O type | `RT0003` |
| Calling a method that is neither `[PureMethod]` nor on a known-pure type | `RT0002` |
| Calling an interface method that is not `[PureMethod]` | `RT0002` |
| Calling a method with an `out` parameter that is not `[PureMethod]` | `RT0002` |

Correctly **not** reported:

| Behaviour | Why |
|---|---|
| Reading a `static readonly` field | Cannot change after initialisation |
| Reading a `const` field | Compile-time constant |
| Calling another `[PureMethod]` method | The contract is declared |
| Recursive and mutually recursive `[PureMethod]` calls | The contract is declared |
| Generic `[PureMethod]` methods | The contract is declared |
| Arithmetic, `string`, `Math`, `Convert`, LINQ over in-memory sequences | Known-pure types |
| `new DateTime(2020, 1, 1)` | Deterministic construction |

## What v1.0 does NOT guarantee

**These are false negatives.** Code in these categories can violate referential
transparency without producing any diagnostic. They are recorded here so that consumers
do not mistake silence for a guarantee.

Each is locked by a test named `FALSE_NEGATIVE_*` in `PuritySemanticsTests`, so the gap
is visible in the test suite rather than only in prose.

### 1. Instance state mutation

The RT analyzer inspects **static** field references only. Mutating instance state from a
`[PureMethod]` method is not detected.

```csharp
private int _f;
private int P { get; set; }

[PureMethod]
public object Run()
{
    _f = 1;      // NOT detected — observable state change
    P  = 1;      // NOT detected — observable state change
    return null;
}
```

This is the **largest** gap in v1.0. `RT0001` guards static state only; the equivalent
instance-state rule does not exist yet.

### 2. Non-deterministic members of known-pure types

`System.DateTime` and `System.Guid` are on the known-pure type list, so *every* member of
them is permitted — including the non-deterministic ones.

```csharp
[PureMethod]
public object Run() => DateTime.Now;    // NOT detected — non-deterministic
                                        // DateTime.UtcNow, Guid.NewGuid() likewise
```

The known-pure list is **type**-granular. Purity is a property of a member, not of a
type, so any type with both deterministic and non-deterministic members is approximated
incorrectly.

### 3. Mutation through references

Writing through an array element or a mutable collection obtained inside the method is
not detected.

```csharp
[PureMethod]
public object Run()
{
    var a = new int[1];
    a[0] = 5;    // NOT detected
    return a;
}
```

Locals are not tracked, so PureSharp cannot tell a freshly allocated array (harmless)
from one reachable by the caller (observable).

### 4. Delegate and lambda invocation

`System.Func`, `System.Action`, `System.Predicate`, and `System.Comparison` are treated as
known-pure, so **invoking** a delegate is always permitted regardless of what it points at.

```csharp
[PureMethod]
public object Run()
{
    Func<int> f = () => 1;
    return f();   // NOT detected, whatever f actually does
}
```

A delegate's purity depends on its target, which is not known statically in general.

### 5. Control-flow boundaries

`throw`, `try`/`catch`, and object construction are not inspected.

```csharp
[PureMethod]
public object Run() => throw new InvalidOperationException();  // NOT detected
```

Throwing is arguably compatible with referential transparency (the same input yields the
same exception), but a constructor running arbitrary side effects is not. Neither case is
analysed in v1.0.

### 6. Cross-assembly and interprocedural reach

An unmarked method is reported (`RT0002`) without inspecting its body, so PureSharp never
concludes that an unmarked method *is* pure. The reach of the `[PureMethod]` contract
across call boundaries is specified separately — see Issue #10.

### 7. Local functions

A local function declared inside a `[PureMethod]` method is analysed as part of the outer
method, so a static-state access inside it **is** reported (`RT0001`). But the local
function itself carries no `[PureMethod]` attribute, so **calling it** is reported as a
non-pure call (`RT0002`).

```csharp
[PureMethod]
public int Read()
{
    int Inner() => Counter;   // RT0001 on Counter
    return Inner();           // RT0002 on Inner()
}
```

This double reporting is a consequence of the attribute-based contract, not a deliberate
design. Its resolution belongs to Issue #10.

## False positive candidates

No confirmed false positive is known at the time of writing. The structural risks are:

| Risk | Why it could over-report |
|---|---|
| Local function invocation | Reported `RT0002` even though the body is analysed as part of the pure method (gap 7). This is the most likely source of user-visible noise. |
| Known-pure list omissions | A genuinely pure BCL type absent from the list produces `RT0002`. The list is an allowlist, so every omission over-reports. |
| I/O type prefix matching | I/O detection is a string prefix match on the containing type's display name. A user type in a namespace beginning `System.Net.` or `NLog.` would be misclassified as I/O. |

The last two follow from the approximation being name-based rather than semantic; they
are the cost of keeping the analysis decidable and fast.

## Diagnostic message alignment

Each RT message must state the rule it enforces, in the terms of this document.

| ID | Message | Aligned? |
|---|---|---|
| `RT0001` | Cannot access static, non-readonly field `'{1}'` within `[PureMethod]` method `'{0}'` | Yes — names the exact condition (static, non-readonly) rather than "impure field" |
| `RT0002` | Cannot call non-pure method `'{1}'` within `[PureMethod]` method `'{0}'` | Yes — "non-pure" is defined here as "neither `[PureMethod]` nor a known-pure type" |
| `RT0003` | Cannot perform I/O operations within `[PureMethod]` method `'{0}'` | Yes, with a caveat: it does not name *which* operation. Adding the member name would improve it, and is a non-breaking change under `DIAGNOSTICS.md` since message text is not contract surface. |

## Summary for consumers

`[PureMethod]` in v1.0 is a **useful but partial** guarantee. It reliably catches static
mutable state, I/O, and calls into unmarked code. It does **not** currently catch instance
state mutation, non-deterministic members of otherwise-pure types, mutation through
references, or delegate targets.

Treat a clean RT analysis as "no violation of the guaranteed subset was found", never as
"this method is proven referentially transparent".

## Open decisions

These require a product decision and would each be a **breaking change** under the
compatibility rules in `DIAGNOSTICS.md` (widening detection → major release). They are
recorded here rather than implemented, so that the v1.0 guarantee boundary stays explicit
and stable.

1. **Instance state mutation** — add an instance-state counterpart to `RT0001`? This is
   the largest gap and the most likely to surprise users.
2. **Member-granular purity** — replace the type-granular known-pure list with member
   granularity, so `DateTime.Now` and `Guid.NewGuid()` are rejected while
   `new DateTime(...)` and `Guid.Parse(...)` remain allowed.
3. **Local function contract** — should a local function inherit the enclosing method's
   `[PureMethod]` contract, removing the `RT0002` on its own invocation? (Issue #10)
4. **I/O classification** — replace prefix matching with a more precise mechanism to
   remove the namespace-collision false positive risk.

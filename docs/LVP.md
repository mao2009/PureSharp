# LVP v1 immutability contract

This document defines the behavioral boundary for the LVP rule family in v1.0. Diagnostic IDs, categories, and default severities remain defined by `docs/DIAGNOSTICS.md`.

## Immutable-local marker

A normal local variable whose name begins with `_` is treated as immutable after its initial value is established. The single `_` discard is never an immutable local.

## LVP0001 — mutation after initialization

LVP0001 is an `Error` and is reported when an underscore-prefixed immutable local is mutated after initialization through any of these supported write forms:

- simple assignment;
- compound assignment;
- increment or decrement;
- passing the local as a `ref` or `out` argument;
- deconstruction assignment when the local appears in the assignment target;
- any of the above when the local is captured by a lambda or local function.

Passing an immutable local as an `in` argument is allowed because the callee cannot write through that argument.

## LVP0002 — initialization at declaration

LVP0002 is an `Error` for a normal underscore-prefixed local declared without an initializer.

`foreach` iteration variables are an explicit exception: their value is supplied by the iteration protocol, so the absence of a declarator initializer must not produce LVP0002.

Pattern variables are not declaration-time immutable locals for LVP0001/LVP0002. Their lifetime and initialization are controlled by pattern matching syntax rather than a normal local declarator.

## LVP0003 — naming suggestion

LVP0003 is a `Warning`. It suggests `_` naming for ordinary locals that are effectively immutable.

To avoid noisy or misleading suggestions, the naming analyzer excludes special syntax-owned locals such as catch variables, `using` resources/declarations, and `foreach` control variables. Pattern variables are likewise outside the ordinary-local suggestion boundary.

`ref`/`out`, assignment, compound assignment, coalescing assignment, and increment/decrement count as writes and therefore suppress LVP0003 for the affected local.

## Generated and special locals

Generated code is not analyzed. Discards are ignored. Fields are not locals and are never governed by LVP rules even when their names start with `_`.

## Edge-case regression coverage

Analyzer tests pin the following v1 boundaries:

- ordinary declaration-time initialization;
- missing initialization;
- simple/compound/increment mutation;
- `ref`, `out`, and allowed `in` arguments;
- `foreach` implicit initialization;
- pattern-variable exclusion;
- deconstruction assignment;
- captured locals mutated from lambdas and local functions;
- discard and field exclusion.

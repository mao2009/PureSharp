# Fluent.If v1 contract

This document defines the v1 behavior of `PureSharp.Core.Fluent.If` and the boundary between the runtime API and `FIF0001` analyzer enforcement.

## v1 decision

`Fluent.If` remains a **core v1 feature**. It provides expression-oriented conditional branching while the analyzer prevents syntactically incomplete chains from silently escaping as `ConditionResult<T>` or `ConditionAction` values.

## Runtime API

### Result chains

```csharp
var value = Fluent.If(condition1, () => first)
    .ElseIf(condition2, () => second)
    .Else(() => fallback);
```

`Fluent.If<T>(bool, Func<T>)` returns `ConditionResult<T>`. `ElseIf` preserves that intermediate type; `Else(Func<T>)` and `Else(T)` terminate the chain and return `T`.

### Action chains

```csharp
Fluent.If(condition1, () => DoFirst())
    .ElseIf(condition2, () => DoSecond())
    .Else(() => DoFallback());
```

The action overload returns `ConditionAction`. Its `Else` terminator returns `void`.

## Evaluation and exception semantics

- Conditions are evaluated by normal C# rules before the method call.
- A selected `Func<T>` or `Action` is invoked exactly once.
- Delegates belonging to branches skipped because a previous branch resolved are not invoked.
- `ElseIf` stops evaluating branch delegates after the first matching branch.
- `Else` evaluates its delegate only when no earlier branch resolved.
- Exceptions thrown by a selected delegate propagate unchanged. Fluent.If does not catch, wrap, retry, or translate them.
- A null delegate is only dereferenced if its branch is selected. PureSharp does not add a separate null-argument contract in v1.

## Analyzer responsibility

`FIF0001` is an `Error` and requires each syntactic chain that starts at `PureSharp.Core.Fluent.If(...)` to reach the corresponding `ConditionResult<T>.Else(...)` or `ConditionAction.Else(...)` call before the intermediate condition object is consumed by anything else.

The analyzer deliberately keys off the resolved `Fluent.If` symbol rather than merely inspecting return types. This prevents unrelated APIs that happen to return `ConditionResult<T>` from being treated as Fluent.If chains.

`Else(...)` is the boundary of the FluentIf chain. For result chains, the value returned by `Else(...)` is an ordinary `T` and may immediately participate in further member access or method calls without being part of FIF0001 analysis.

### Reported

```csharp
Fluent.If(flag, () => 1);                         // missing Else
Fluent.If(flag, () => 1).ElseIf(other, () => 2); // missing Else
var chain = Fluent.If(flag, () => 1);             // intermediate value escapes
Fluent.If(flag, () => 1).ToString();              // intermediate object consumed before Else
```

Nested and lambda-contained chains are analyzed independently.

### Accepted

```csharp
Fluent.If(flag, () => 1).Else(0);
Fluent.If(flag, () => 1).Else(() => 0);
Fluent.If(flag, () => { }).Else(() => { });
((Fluent.If(flag, () => 1))).Else(0);
Fluent.If(flag, () => 1).Else(0).ToString(); // ordinary chaining after termination
```

Explicit generic type arguments and ordinary generic type inference are both supported.

## Invalid or partially constructed code

When the initial `Fluent.If` call resolves but a chained invocation cannot be bound because the source is temporarily invalid, PureSharp leaves that compiler error to Roslyn rather than adding a potentially misleading `FIF0001`. Once the chained invocation becomes valid, the normal termination rule applies.

A bare, otherwise-valid `Fluent.If(...)` invocation is not considered partial compiler input: it is a valid C# expression and therefore receives `FIF0001`.

## Non-goals for v1

- Persisting a `ConditionResult<T>`/`ConditionAction` and terminating it in a later statement.
- Recognizing user-defined extension methods as alternate chain terminators.
- Swallowing or translating exceptions from branch delegates.
- Replacing the C# `if` statement; Fluent.If is an opt-in expression/control-flow helper.

# PureSharp v1 diagnostic examples

This document provides minimal examples for every diagnostic in the v1 public contract. The canonical ID/category/severity contract remains [`DIAGNOSTICS.md`](DIAGNOSTICS.md).

## RT0001 — static mutable state in `[PureMethod]`

```csharp
using PureSharp.Core;

class Counter
{
    private static int Value;

    [PureMethod]
    public int Read() => Value; // RT0001
}
```

Use immutable state, pass the value as an argument, or move the operation outside the pure contract.

## RT0002 — non-pure method call in `[PureMethod]`

```csharp
using PureSharp.Core;

class Example
{
    private int Load() => 1;

    [PureMethod]
    public int Run() => Load(); // RT0002
}
```

Calls are accepted when the statically resolved method symbol carries `[PureMethod]` or belongs to a known-pure type. See [`CALL-CONTRACT.md`](CALL-CONTRACT.md).

## RT0003 — I/O in `[PureMethod]`

```csharp
using PureSharp.Core;

class Example
{
    [PureMethod]
    public int Run()
    {
        System.Console.WriteLine("side effect"); // RT0003
        return 1;
    }
}
```

Perform I/O outside the pure method and pass its result in as data.

## LVP0001 — reassignment to an immutable local

```csharp
void Run()
{
    var _value = 1;
    _value = 2; // LVP0001
}
```

The rule also covers compound/increment/decrement assignment, `ref`/`out` mutation, and deconstruction reassignment. `in` arguments do not mutate the local.

## LVP0002 — immutable local without declaration-time initialization

```csharp
void Run()
{
    int _value; // LVP0002
}
```

Initialize underscore-prefixed locals when they are declared. Language constructs with implicit initialization, such as `foreach` variables, are not treated as this declaration form.

## LVP0003 — effectively immutable local without the naming convention

```csharp
void Run()
{
    var value = 1; // LVP0003 warning: consider _value
    System.Console.WriteLine(value);
}
```

This is advisory (`Warning` by default). Mutable locals are not reported by LVP0003.

## FIF0001 — incomplete `Fluent.If` chain

```csharp
using PureSharp.Core;

void Run(bool condition)
{
    Fluent.If(condition, () => 1); // FIF0001
}
```

Terminate the chain:

```csharp
var _value = Fluent.If(condition, () => 1)
    .Else(0);
```

`ElseIf` is still intermediate and therefore also requires a final `Else`. Nested chains and chains inside lambdas are analyzed independently. See [`FLUENT_IF.md`](FLUENT_IF.md).

## Configuration

All examples use the default v1 severities. Override an individual rule with Roslyn's standard `.editorconfig` syntax:

```editorconfig
[*.cs]
dotnet_diagnostic.LVP0003.severity = none
```

Do not match on diagnostic message text. IDs are the stable consumer-facing identity.

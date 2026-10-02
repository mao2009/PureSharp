# Getting started with PureSharp v1

This guide is the reproducible consumer path for PureSharp v1. The package contains both the runtime API (`PureSharp.Core`) and the Roslyn analyzers.

## Prerequisites

- A .NET SDK whose compiler hosts Roslyn 4.0 or newer.
- C#.
- A target framework that can reference `netstandard2.0`.

The release CI validates a clean `net10.0` consumer. See [`COMPATIBILITY.md`](COMPATIBILITY.md) for the complete support policy.

## 1. Create a clean project

```bash
dotnet new console -n PureSharpDemo -f net10.0
cd PureSharpDemo
dotnet add package loach.PureSharp
```

For reproducible production builds, pin the package version in the generated project file rather than relying on the latest version.

## 2. Replace `Program.cs`

```csharp
using PureSharp.Core;

var _result = Demo.Compute(2, 3);
System.Console.WriteLine(_result);

internal static class Demo
{
    [PureMethod]
    private static int Add(int a, int b) => a + b;

    public static int Compute(int a, int b)
    {
        var _sum = Add(a, b);
        return Fluent.If(_sum > 0, () => _sum)
            .Else(0);
    }
}
```

Build it:

```bash
dotnet build
```

The example uses all three public concepts without producing a PureSharp error:

- `[PureMethod]` declares a purity contract.
- `_sum` and `_result` opt into immutable-local naming.
- `Fluent.If(...).Else(...)` forms a terminated FluentIf chain.

## 3. Confirm analyzer discovery

Introduce a violation inside `Add`:

```csharp
private static int Counter;

[PureMethod]
private static int Add(int a, int b) => a + b + Counter;
```

`dotnet build` must now report `RT0001` and fail because `RT0001` defaults to `Error`.

## 4. Configure diagnostics with `.editorconfig`

PureSharp uses Roslyn's standard diagnostic configuration; there is no custom rule-selection file.

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

To stage adoption, override individual IDs. For example:

```editorconfig
[*.cs]
dotnet_diagnostic.LVP0003.severity = none
```

The release pipeline verifies the same mechanism by building a clean project once with `RT0001` active and once with `RT0001` suppressed.

## Next reading

- [`DIAGNOSTICS.md`](DIAGNOSTICS.md) — public diagnostic contract and compatibility rules.
- [`RULE-EXAMPLES.md`](RULE-EXAMPLES.md) — examples for all seven v1 diagnostics.
- [`PURITY-SEMANTICS.md`](PURITY-SEMANTICS.md) — what v1 purity does and does not guarantee.
- [`CALL-CONTRACT.md`](CALL-CONTRACT.md) — `[PureMethod]` call-boundary rules.
- [`LVP.md`](LVP.md) — immutable-local boundaries.
- [`FLUENT_IF.md`](FLUENT_IF.md) — FluentIf runtime and termination contract.
- [`MIGRATION-1.0.md`](MIGRATION-1.0.md) — upgrading from 0.x.

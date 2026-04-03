# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

[![CI & NuGet Upload](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml/badge.svg)](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml) [![NuGet](https://img.shields.io/nuget/v/loach.PureSharp.svg)](https://www.nuget.org/packages/loach.PureSharp) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![X (Twitter) Follow](https://img.shields.io/twitter/follow/loach_mao)](https://x.com/loach_mao)

**PureSharp** is a toolset designed to strongly support "referential transparency" and "immutability" in C#, bringing the safety of functional programming to C#. It leverages Roslyn analyzers to enforce robust, bug-resistant code writing at the compilation level.

## Core Concepts

1.  **Purity Enforcement:** Explicitly declare logic without side effects and verify it mechanically.
2.  **Immutability Introduction:** Achieve "non-reassignable local variables," a feature lacking in the language specification, through intuitive naming conventions.
3.  **Safe Control Flow:** Provide pipeline-style conditional branching to prevent runtime exceptions and oversights.

## Key Features

### 1. PureSharp.Core
Provides fundamental attributes and utilities.

*   **`[PureMethod]` attribute**: Declares a method as "pure" (referentially transparent).
*   **`Fluent.If`**: A fluent interface that allows `if-else` statements to be written as expressions.

### 2. PureSharp.Analyzers (Roslyn Analyzers)
Monitors code writing in real-time and reports rule violations.

#### Referential Transparency Verification (RTxxxx)
An `Error` is reported when a method annotated with `[PureMethod]` performs the following operations:
*   Accessing static, mutable (non-readonly) fields.
*   Calling non-pure methods (methods without `[PureMethod]`).
*   I/O operations (access to Console, File, Network, etc.).

#### Local Variable Immutability Enforcement (LVPxxxx)
Achieves immutability using a naming convention that starts with an underscore (`_`).
*   **Mandatory Immutability**: Prohibits reassignment to local variables starting with `_` (Error).
*   **Initialization Enforcement**: Variables starting with `_` must be initialized at the time of declaration (Error).
*   **Naming Suggestion**: Suggests adding an underscore (`_`) to variables that have never been reassigned (Warning).

#### FluentIf Termination Check (FIFxxxx)
*   Verifies that chains starting with `Fluent.If` are correctly terminated with `.Else()`. Failure to terminate will result in a compile error.

---

## Quick Start

### 1. Writing Referentially Transparent Methods
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // Mutable static field

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: Calculation only
        return a + b; 
        
        // NG: Accessing static field causes RT0001 error
        // _globalCache = a + b; 
        
        // NG: I/O operations cause RT0003 error
        // Console.WriteLine(a); 
    }
}
```

### 2. Utilizing Immutable Local Variables
```csharp
public void Process()
{
    int _result = Calculate(); // Declared as an immutable variable
    
    // NG: Attempting to reassign causes LVP0001 error
    // _result = 10; 
    
    int count = 0; // Regular variable
    // Suggestion: If not reassigned, a warning to change to "_count" (LVP0003)
}
```

### 3. Safe Conditional Branching (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // Forgetting .Else() causes FIF0001 error
```

---

## Project Structure

*   **PureSharp.Core**: Provides core attributes and runtime libraries (.NET 10.0 / netstandard2.0).
*   **PureSharp.Analyzers**: The main Roslyn analyzer (netstandard2.0).
*   **PureSharp.Analyzers.Tests**: Unit tests to verify analyzer behavior (xUnit).

---

## Motivation for Development
C# is a very powerful language, but in large-scale development or complex logic, debugging can become difficult due to unintended side effects or variable reuse. PureSharp was born to provide developers with "freedom (from bugs)" in the form of "constraints."

---
## License
This project is released under the MIT License.

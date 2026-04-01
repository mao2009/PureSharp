# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

**PureSharp** 'oH C# neH "referential transparency" 'ej "immutability" nIjtay' lI' ghajbogh pat, C# vo' functional programming lot lI'moH. Roslyn analyzers lo' ghot compilation level neH robust, bug-resistant ghunwI' writing enforce.

## Concepts tIn

1.  **Purity Enforce:** Side effects ghajbe'bogh logic declare chu' 'ej mechanically verify.
2.  **Immutability lI'moH:** "non-reassignable local variables" chav, language specification neH feature missing, intuitive naming conventions vegh.
3.  **Safe Control Flow:** Pipeline-style conditional branching nob runtime exceptions 'ej oversights botmeH.

## Features nIjtay'

### 1. PureSharp.Core
Fundamental attributes 'ej utilities nob.

*   **`[PureMethod]` attribute**: Method declare "pure" (referentially transparent) ghaH.
*   **`Fluent.If`**: Expression rur `if-else` statements ghunmeH fluent interface nob.

### 2. PureSharp.Analyzers (Roslyn Analyzers)
Real-time neH ghunwI' writing monitor 'ej rule violations report.

#### Referential Transparency Verification (RTxxxx)
`[PureMethod]` ghajbogh method tlhe' ghot `Error` report:
*   Static, mutable (non-readonly) fields access.
*   Non-pure methods (methods `[PureMethod]` ghajbe'bogh) pong.
*   I/O operations (Console, File, Network, etc. access).

#### Local Variable Immutability Enforcement (LVPxxxx)
Underscore (`_`) lo'bogh naming convention vegh immutability chav.
*   **Mandatory Immutability**: `_` lo'bogh local variables reassign bot (Error).
*   **Initialization Enforcement**: `_` lo'bogh variables declaration ghunmeH initialize (Error).
*   **Naming Suggestion**: Reassignbe'bogh variablesvaD underscore (`_`) chel suggest (Warning).

#### FluentIf Termination Check (FIFxxxx)
*   `Fluent.If` tlhe'bogh chains `.Else()` lo'bogh correctly terminate verify. Terminatebe'chugh compile error ghot.

---

## Quick Start

### 1. Referentially Transparent Methods ghun
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // Mutable static field

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: Calculation neH
        return a + b; 
        
        // NG: Static field access RT0001 error ghot
        // _globalCache = a + b; 
        
        // NG: I/O operations RT0003 error ghot
        // Console.WriteLine(a); 
    }
}
```

### 2. Immutable Local Variables lo'
```csharp
public void Process()
{
    int _result = Calculate(); // Immutable variable rur declare
    
    // NG: Reassign LVP0001 error ghot
    // _result = 10; 
    
    int count = 0; // Regular variable
    // Suggestion: Reassignbe'chugh, "_count" chel Warning (LVP0003)
}
```

### 3. Safe Conditional Branching (FluentIf) lo'
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // .Else() lulchugh FIF0001 error ghot
```

---

## Project Structure

*   **PureSharp.Core**: Core attributes 'ej runtime libraries (.NET 10.0 / netstandard2.0) nob.
*   **PureSharp.Analyzers**: Roslyn analyzer tIn (netstandard2.0).
*   **PureSharp.Analyzers.Tests**: Analyzer behavior verifymeH unit tests (xUnit).

---

## Motivation for Development
C# 'oH language HoS, 'ach large-scale development ghap complex logic neH, unintended side effects ghap variable reusemo' debugging Qatlh ghot. PureSharp bogh "constraints" vegh ghunwI'vaD "freedom (bugs vo')" nobmeH.

---
## License
MIT License lo'bogh project 'oH.

# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

**PureSharp** 是一个旨在强力支持 C# 中的“引用透明性”和“不可变性”的工具集，将函数式编程的安全性带入 C#。它利用 Roslyn 分析器在编译级别强制执行健壮、防虫的代码编写。

## 核心概念

1.  **强制纯粹性 (Purity)**：明确声明无副作用的逻辑并进行机械验证。
2.  **引入不可变性 (Immutability)**：通过直观的命名约定实现语言规范中缺失的“不可重分配局部变量”。
3.  **安全控制流 (Safe Flow)**：提供管道式条件分支，防止运行时异常和疏忽。

## 主要功能

### 1. PureSharp.Core
提供基础特性和实用工具。

*   **`[PureMethod]` 特性**：将方法声明为“纯粹”（引用透明）。
*   **`Fluent.If`**：一个流畅的接口，允许将 `if-else` 语句编写为表达式。

### 2. PureSharp.Analyzers (Roslyn 分析器)
实时监控代码编写并报告规则违规。

#### 引用透明性验证 (RTxxxx)
当带有 `[PureMethod]` 注解的方法执行以下操作时，将报告“错误 (Error)”：
*   访问静态、可变（非只读）字段。
*   调用非纯方法（没有 `[PureMethod]` 的方法）。
*   I/O 操作（访问控制台、文件、网络等）。

#### 局部变量不可变性强制 (LVPxxxx)
使用以下划线 (`_`) 开头的命名约定来实现不可变性。
*   **强制不可变性**：禁止对以下划线 `_` 开头的局部变量进行重新赋值（错误）。
*   **强制初始化**：以下划线 `_` 开头的变量必须在声明时初始化（错误）。
*   **命名建议**：建议对从未被重新赋值的变量添加下划线 (`_`) 前缀（警告）。

#### FluentIf 终止检查 (FIFxxxx)
*   验证以 `Fluent.If` 开始的链式调用是否以 `.Else()` 正确终止。未能终止将导致编译错误。

---

## 快速上手

### 1. 编写引用透明的方法
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // 可变静态字段

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: 仅进行计算
        return a + b; 
        
        // NG: 访问静态字段会导致 RT0001 错误
        // _globalCache = a + b; 
        
        // NG: I/O 操作会导致 RT0003 错误
        // Console.WriteLine(a); 
    }
}
```

### 2. 利用不可变局部变量
```csharp
public void Process()
{
    int _result = Calculate(); // 声明为不可变变量
    
    // NG: 尝试重新赋值会导致 LVP0001 错误
    // _result = 10; 
    
    int count = 0; // 普通变量
    // 建议：如果没有重新赋值，会提示将其更改为 "_count" (LVP0003)
}
```

### 3. 安全条件分支 (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // 忘记 .Else() 会导致 FIF0001 错误
```

---

## 项目结构

*   **PureSharp.Core**：提供核心特性和运行时库（.NET 10.0 / netstandard2.0）。
*   **PureSharp.Analyzers**：主要的 Roslyn 分析器（netstandard2.0）。
*   **PureSharp.Analyzers.Tests**：用于验证分析器行为的单元测试（xUnit）。

---

## 开发动机
C# 是一种非常强大的语言，但在大规模开发或复杂逻辑中，由于意外的副作用或变量重用，调试可能会变得困难。PureSharp 的诞生是为了以“约束”的形式为开发者提供“自由（免于 bug）”。

---
## 许可证
该项目根据 MIT 许可证发布。

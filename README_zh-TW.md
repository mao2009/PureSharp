# PureSharp

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

[![CI & NuGet Upload](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml/badge.svg)](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml) [![NuGet](https://img.shields.io/nuget/v/loach.PureSharp.svg)](https://www.nuget.org/packages/loach.PureSharp) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![X (Twitter) Follow](https://img.shields.io/twitter/follow/loach_mao)](https://x.com/loach_mao)

**PureSharp** 是一個旨在強力支持 C# 中的「引用透明性」與「不可變性」的工具集，將函數式編程的安全性帶入 C#。它利用 Roslyn 分析器在編譯級別強制執行健壯、防蟲的代碼編寫。

## 核心概念

1.  **強制純粹性 (Purity)**：明確聲明無副作用的邏輯並進行機械驗證。
2.  **引入不可變性 (Immutability)**：通過直觀的命名約定實現語言規範中缺失的「不可重分配局部變數」。
3.  **安全控制流 (Safe Flow)**：提供管道式條件分支，防止運行時異常和疏忽。

## 主要功能

### 1. PureSharp.Core
提供基礎特性和實用工具。

*   **`[PureMethod]` 特性**：將方法聲明為「純粹」（引用透明）。
*   **`Fluent.If`**：一個流暢的接口，允許將 `if-else` 語句編寫為表達式。

### 2. PureSharp.Analyzers (Roslyn 分析器)
實時監控代碼編寫並報告規則違規。

#### 引用透明性驗證 (RTxxxx)
當帶有 `[PureMethod]` 註解的方法執行以下操作時，將報告「錯誤 (Error)」：
*   存取靜態、可變（非唯讀）欄位。
*   調用非純方法（沒有 `[PureMethod]` 的方法）。
*   I/O 操作（存取控制台、檔案、網路等）。

#### 局部變數不可變性強制 (LVPxxxx)
使用以下劃線 (`_`) 開頭的命名約定來實現不可變性。
*   **強制不可變性**：禁止對以下劃線 `_` 開頭的局部變數進行重新賦值（錯誤）。
*   **強制初始化**：以下劃線 `_` 開頭的變數必須在聲明時初始化（錯誤）。
*   **命名建議**：建議對從未被重新賦值的變數添加下劃線 (`_`) 前綴（警告）。

#### FluentIf 終止檢查 (FIFxxxx)
*   驗證以 `Fluent.If` 開始的鏈式調用是否以 `.Else()` 正確終止。未能終止將導致編譯錯誤。

---

## 快速上手

### 1. 編寫引用透明的方法
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // 可變靜態欄位

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: 僅進行計算
        return a + b; 
        
        // NG: 存取靜態欄位會導致 RT0001 錯誤
        // _globalCache = a + b; 
        
        // NG: I/O 操作會導致 RT0003 錯誤
        // Console.WriteLine(a); 
    }
}
```

### 2. 利用不可變局部變數
```csharp
public void Process()
{
    int _result = Calculate(); // 聲明為不可變變數
    
    // NG: 嘗試重新賦值會導致 LVP0001 錯誤
    // _result = 10; 
    
    int count = 0; // 普通變數
    // 建議：如果沒有重新賦值，會提示將其更改為 "_count" (LVP0003)
}
```

### 3. 安全條件分支 (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // 忘記 .Else() 會導致 FIF0001 錯誤
```

---

## 項目結構

*   **PureSharp.Core**：提供核心特性和運行時庫（.NET 10.0 / netstandard2.0）。
*   **PureSharp.Analyzers**：主要的 Roslyn 分析器（netstandard2.0）。
*   **PureSharp.Analyzers.Tests**：用於驗證分析器行為的單元測試（xUnit）。

---

## 開發動機
C# 是一種非常強大的語言，但在大規模開發或複雜邏輯中，由於意外的副作用或變數重用，調試可能會變得困難。PureSharp 的誕生是為了以「約束」的形式為開發者提供「自由（免於 bug）」。

---
## 許可證
該項目根據 MIT 許可證發佈。

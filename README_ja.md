# PureSharp (日本語版)

[English](README.md) | [日本語](README_ja.md) | [简体中文](README_zh-CN.md) | [繁體中文](README_zh-TW.md) | [Esperanto](README_eo.md) | [Klingon](README_tlh.md) | [Español](README_es.md) | [Français](README_fr.md) | [Deutsch](README_de.md) | [한국어](README_ko.md)

[![CI & NuGet Upload](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml/badge.svg)](https://github.com/mao2009/PureSharp/actions/workflows/upload_nuget.yml) [![NuGet](https://img.shields.io/nuget/v/loach.PureSharp.svg)](https://www.nuget.org/packages/loach.PureSharp) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![X (Twitter) Follow](https://img.shields.io/twitter/follow/loach_mao)](https://x.com/loach_mao)

**PureSharp** は、Roslyn Analyzer と小さなランタイムAPIによって、C# に「参照透過性」「不変ローカル変数」「安全な FluentIf 終端」の契約を導入するツールです。

## インストール

```bash
dotnet add package loach.PureSharp
```

1つのパッケージに `PureSharp.Core` と Analyzer が含まれます。独自設定ファイルは使わず、Roslyn標準の `.editorconfig` で Diagnostic 単位に設定します。

clean project からの導入手順は [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md) を参照してください。

## 主要機能

### `[PureMethod]`

```csharp
using PureSharp.Core;

public static class Calculator
{
    [PureMethod]
    public static int Add(int a, int b) => a + b;
}
```

`[PureMethod]` 内では、v1契約で定義された範囲の static mutable state、非pureメソッド呼び出し、I/O を検出します。ただし PureSharp は「すべての副作用を証明可能に検出する」ものではありません。正確な保証範囲は [`docs/PURITY-SEMANTICS.md`](docs/PURITY-SEMANTICS.md) と [`docs/CALL-CONTRACT.md`](docs/CALL-CONTRACT.md) がSSOTです。

### 不変ローカル変数

```csharp
var _value = Calculate();
// _value = 10; // LVP0001
```

`_` で始まるローカル変数は宣言時初期化が必要で、v1で対象となる再代入経路では変更できません。詳細は [`docs/LVP.md`](docs/LVP.md) を参照してください。

### FluentIf

```csharp
var _status = Fluent.If(score >= 80, () => 1)
    .ElseIf(score >= 60, () => 2)
    .Else(0);
```

`Fluent.If(...)` から始まるチェーンは対応する `.Else(...)` まで到達する必要があります。ネスト、lambda、generic推論、short-circuit、例外伝播の仕様は [`docs/FLUENT_IF.md`](docs/FLUENT_IF.md) にあります。

## v1 公開 Diagnostic

| Diagnostic ID | Category | 内容 | 既定 severity |
|---|---|---|---|
| **RT0001** | Purity | `[PureMethod]` から static mutable field へアクセス | Error |
| **RT0002** | Purity | `[PureMethod]` から non-pure method を呼び出す | Error |
| **RT0003** | Purity | `[PureMethod]` 内の I/O | Error |
| **LVP0001** | Purity | 不変ローカル変数への再代入 | Error |
| **LVP0002** | Purity | 不変ローカル変数の宣言時初期化忘れ | Error |
| **LVP0003** | Naming | 実質不変なローカル変数への命名提案 | Warning |
| **FIF0001** | FluentIf | FluentIf chain の `.Else(...)` 終端忘れ | Error |

DiagnosticのID・Category・既定severity・互換性ポリシーは [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md) がSSOTです。7ルールすべての最小例は [`docs/RULE-EXAMPLES.md`](docs/RULE-EXAMPLES.md) にあります。

## `.editorconfig`

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

段階導入するときはID単位で変更します。

```editorconfig
[*.cs]
dotnet_diagnostic.LVP0003.severity = none
```

## 0.x から 1.0 への移行

[`docs/MIGRATION-1.0.md`](docs/MIGRATION-1.0.md) を参照してください。Diagnostic IDは7つともv1公開IDとして維持されますが、`ref`/`out`、deconstruction reassignment、FluentIf終端判定など、0.xより厳密になった境界があります。

## 対応範囲と既知の制約

PureSharp.Core / Analyzer は `netstandard2.0` を対象にします。C# / Roslyn / consumer TFM / performance gate の方針は [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md) にあります。

RT Analyzer が何も報告しなかった場合も、それは「v1で保証する検出範囲に違反が見つからなかった」ことを意味し、形式的に完全な参照透過性を証明したことを意味しません。

## ドキュメント

- [`docs/GETTING_STARTED.md`](docs/GETTING_STARTED.md) — clean consumer Quick Start
- [`docs/DIAGNOSTICS.md`](docs/DIAGNOSTICS.md) — Diagnostic SSOT / 互換性方針
- [`docs/RULE-EXAMPLES.md`](docs/RULE-EXAMPLES.md) — 全7 Diagnostic の例
- [`docs/PURITY-SEMANTICS.md`](docs/PURITY-SEMANTICS.md) — purity semantics / known limitations
- [`docs/CALL-CONTRACT.md`](docs/CALL-CONTRACT.md) — `[PureMethod]` call contract
- [`docs/LVP.md`](docs/LVP.md) — 不変ローカル変数
- [`docs/FLUENT_IF.md`](docs/FLUENT_IF.md) — FluentIf仕様
- [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md) — 対応バージョン / performance
- [`docs/MIGRATION-1.0.md`](docs/MIGRATION-1.0.md) — 0.x→1.0 migration
- [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md) — README / 翻訳のSSOT方針
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — contribution guide

## README翻訳について

挙動契約は英語README単独ではなく、`docs/DIAGNOSTICS.md` と各専門contract documentを正本とします。日本語を含む翻訳READMEは利用者向け説明であり、矛盾した場合はcontract documentが優先されます。詳細は [`docs/DOCUMENTATION.md`](docs/DOCUMENTATION.md) を参照してください。

## ライセンス

MIT License です。

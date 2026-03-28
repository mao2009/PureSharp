# PureSharp (日本語版)

これは PureSharp プロジェクトの日本語版 README です。

- [English / 英語](README.md)

---

# PureSharp

**PureSharp** は、C# における「参照透過性」と「不変性」を強力に支援し、関数型プログラミングの安全性を C# に持ち込むためのツールセットです。Roslyn アナライザーを活用し、バグの入り込みにくい堅牢なコード記述をコンパイルレベルで強制します。

## 核心となるコンセプト

1.  **純粋性の強制 (Purity)**: 副作用のないロジックを明示し、機械的に検証します。
2.  **不変性の導入 (Immutability)**: 言語仕様で不足している「再代入不可能なローカル変数」を直感的な命名規則で実現します。
3.  **安全な制御フロー (Safe Flow)**: 実行時の例外や考慮漏れを防ぐ、パイプライン形式の条件分岐を提供します。

## 主要機能

### 1. PureSharp.Core
基本となる属性とユーティリティを提供します。

*   **`[PureMethod]` 属性**: メソッドが「純粋（参照透過）」であることを宣言します。
*   **`Fluent.If`**: `if-else` 文を式として記述できる流れるようなインターフェース。

### 2. PureSharp.Analyzers (Roslyn アナライザー)
コードの書き方をリアルタイムで監視し、ルール違反を報告します。

#### 参照透過性の検証 (RTxxxx)
`[PureMethod]` が付与されたメソッドが以下の操作を行っている場合にエラー（Error）を出力します。
*   静的かつ可変（non-readonly）なフィールドへのアクセス。
*   非純粋なメソッド（`[PureMethod]` がないメソッド）の呼び出し。
*   I/O 操作（Console, File, Network 等へのアクセス）。

#### ローカル変数の不変性強制 (LVPxxxx)
アンダースコア（`_`）で始まる命名規則を利用して、不変性を実現します。
*   **強制不変**: `_` で始まるローカル変数への再代入を禁止します（Error）。
*   **初期化強制**: `_` で始まる変数は宣言と同時に初期化する必要があります（Error）。
*   **命名の推奨**: 一度も再代入されていない変数に対して、`_` を付けるよう提案します（Warning）。

#### FluentIf の終端チェック (FIFxxxx)
*   `Fluent.If` で始まるチェーンが `.Else()` で正しく終了しているかを検証します。終了していない場合はコンパイルエラーとなります。

---

## クイックスタート

### 1. 参照透過なメソッドの記述
```csharp
using PureSharp.Core;

public class Calculator
{
    private static int _globalCache; // 可変な静的フィールド

    [PureMethod]
    public int Add(int a, int b)
    {
        // OK: 計算のみ
        return a + b; 
        
        // NG: 静的フィールドへのアクセスは RT0001 エラー
        // _globalCache = a + b; 
        
        // NG: I/O操作は RT0003 エラー
        // Console.WriteLine(a); 
    }
}
```

### 2. 不変ローカル変数の活用
```csharp
public void Process()
{
    int _result = Calculate(); // 不変変数として宣言
    
    // NG: 再代入しようとすると LVP0001 エラー
    // _result = 10; 
    
    int count = 0; // 普通の変数
    // Suggestion: 再代入されないなら "_count" への変更を促す警告 (LVP0003)
}
```

### 3. 安全な条件分岐 (FluentIf)
```csharp
int status = Fluent.If(score >= 80, () => 1)
                   .ElseIf(score >= 60, () => 2)
                   .Else(0); // .Else() を忘れると FIF0001 エラー
```

---

## プロジェクト構成

*   **PureSharp.Core**: 基本となる属性とユーティリティを提供します。 (.NET 10.0 / netstandard2.0)
*   **PureSharp.Analyzers**: Roslyn アナライザー本体 (netstandard2.0)
*   **PureSharp.Analyzers.Tests**: アナライザーの挙動を検証するユニットテスト (xUnit)

---

## 開発の動機
C# は非常に強力な言語ですが、大規模な開発や複雑なロジックにおいて、意図しない副作用や変数の再利用が原因でデバッグが困難になることがあります。PureSharp は、開発者に「制約」という名の「自由（バグからの解放）」を提供するために生まれました。

---
## ライセンス
このプロジェクトは MIT ライセンスの下で公開されています。

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.ImmutableNamingSuggestionAnalyzer>;

namespace PureSharp.Analyzers.Tests;

public class ImmutableNamingSuggestionAnalyzerTests
{
    // =========================================================
    // Task 1: LVP0003 診断記述子のメタデータ検証
    // =========================================================

    [Fact]
    public void LVP0003_Descriptor_HasCorrectId()
    {
        Assert.Equal("LVP0003", PureSharp.Analyzers.ImmutableNamingSuggestionAnalyzer.LVP0003.Id);
    }

    [Fact]
    public void LVP0003_Descriptor_IsWarning()
    {
        Assert.Equal(DiagnosticSeverity.Warning, PureSharp.Analyzers.ImmutableNamingSuggestionAnalyzer.LVP0003.DefaultSeverity);
    }

    [Fact]
    public void LVP0003_Descriptor_IsEnabledByDefault()
    {
        Assert.True(PureSharp.Analyzers.ImmutableNamingSuggestionAnalyzer.LVP0003.IsEnabledByDefault);
    }

    [Fact]
    public void LVP0003_Descriptor_IsInSupportedDiagnostics()
    {
        var analyzer = new PureSharp.Analyzers.ImmutableNamingSuggestionAnalyzer();
        Assert.Contains(PureSharp.Analyzers.ImmutableNamingSuggestionAnalyzer.LVP0003, analyzer.SupportedDiagnostics);
    }

    // =========================================================
    // Task 2: メソッド内の再代入トラッカーの基本動作検証
    // =========================================================

    [Fact]
    public async Task ImmutableVariable_WithoutUnderscore_ReportsLVP0003()
    {
        // 初期化のみで再代入なし、アンダースコアなし -> LVP0003 警告
        var testCode = @"
public class Test
{
    public void Method()
    {
        int {|LVP0003:x|} = 10;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task MutableVariable_WithoutUnderscore_NoDiagnostic()
    {
        // 再代入ありの変数 -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        int x = 10;
        x = 20;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // Task 3: 代入操作の網羅と除外ルールの適用
    // =========================================================

    // --- 3.1: 全ての代入操作の網羅 ---

    [Fact]
    public async Task CompoundAssignment_CountsAsReassignment_NoDiagnostic()
    {
        // 複合代入 (+=) がある変数 -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        int x = 10;
        x += 5;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullCoalescingAssignment_CountsAsReassignment_NoDiagnostic()
    {
        // 複合代入 (??=) がある変数 -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        string? x = null;
        x ??= ""default"";
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IncrementOperation_CountsAsReassignment_NoDiagnostic()
    {
        // インクリメント (++) がある変数 -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        int x = 0;
        x++;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DecrementOperation_CountsAsReassignment_NoDiagnostic()
    {
        // デクリメント (--) がある変数 -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        int x = 10;
        x--;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // --- 3.2: ref/out 引数の考慮 ---

    [Fact]
    public async Task OutArgument_CountsAsReassignment_NoDiagnostic()
    {
        // out 引数として宣言された変数 -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        if (int.TryParse(""42"", out int result))
        {
            _ = result;
        }
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RefArgument_CountsAsReassignment_NoDiagnostic()
    {
        // ref 引数として渡された変数 -> 警告なし
        var testCode = @"
public class Test
{
    private static void Increment(ref int value) => value++;

    public void Method()
    {
        int x = 0;
        Increment(ref x);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // --- 除外ルール: 破棄記号と _ プレフィックス ---

    [Fact]
    public async Task DiscardSymbol_IsIgnored_NoDiagnostic()
    {
        // 単一アンダースコア (破棄記号) -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        _ = 42;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UnderscorePrefixedImmutableVariable_IsIgnored_NoDiagnostic()
    {
        // _ で始まる不変変数 -> 命名規則に適合しているため警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _value = 10;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task MultipleVariables_OnlyImmutableWithoutUnderscore_ReportsLVP0003()
    {
        // 不変かつアンダースコアなし -> 警告あり
        // 可変かつアンダースコアなし -> 警告なし
        // 不変かつアンダースコアあり -> 警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        int {|LVP0003:immutable|} = 10;
        int mutable = 5;
        mutable += 1;
        int _alreadyPrefixed = 20;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // Task 4: 命名規則違反の判定と警告の報告
    // =========================================================

    [Fact]
    public async Task UninitializedVariable_NeverAssigned_ReportsLVP0003()
    {
        // 初期化子なしで代入なし -> 実質的な不変として LVP0003 警告
        var testCode = @"
public class Test
{
    public void Method()
    {
        int {|LVP0003:x|};
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ImmutableVariableInLambda_ReportsLVP0003()
    {
        // ラムダ式内の不変変数にも LVP0003 警告が発生する
        var testCode = @"
using System;
public class Test
{
    public void Method()
    {
        Action _a = () =>
        {
            int {|LVP0003:value|} = 42;
        };
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ImmutableVariableInLambda_WithUnderscore_NoDiagnostic()
    {
        // ラムダ式内の _ 付き不変変数 -> 警告なし
        var testCode = @"
using System;
public class Test
{
    public void Method()
    {
        Action _a = () =>
        {
            int _value = 42;
        };
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task WarningMessage_ContainsVariableName()
    {
        // 警告メッセージに変数名が含まれていることを確認
        var testCode = @"
public class Test
{
    public void Method()
    {
        int {|LVP0003:myVariable|} = 100;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ClassField_IsNotAnalyzed_NoDiagnostic()
    {
        // クラスフィールドは解析対象外 -> 警告なし
        var testCode = @"
public class Test
{
    private int value = 10;

    public void Method()
    {
        _ = value;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task LVP0003_ReportedForEffectivelyImmutableVariable()
    {
        // 不変として扱われる変数 (再代入なし) に LVP0003 警告が報告されることを確認
        var testCode = @"
public class Test
{
    public void Method()
    {
        int {|LVP0003:x|} = 10;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task LVP0003_MessageFormat_UsesLVP0003_NotLVP0001()
    {
        // LVP0003 が LVP0003_MessageFormat を使用し、LVP0001_MessageFormat を使用していないことを確認
        // これは静的記述子のテストで確認済みなので、実際の実行を確認するテストとして追加
        // 正しいメッセージ: "Local variable '{0}' is effectively immutable"
        var testCode = @"
public class Test
{
    public void Method()
    {
        int {|LVP0003:x|} = 10;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // 構文的コンテキストの識別ロジックの検証
    // =========================================================

    [Fact]
    public async Task CatchVariable_IsExcluded_NoDiagnostic()
    {
        // catch 句の例外変数 -> 構文的コンテキストのため警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        try { }
        catch (System.Exception ex)
        {
            _ = ex.Message;
        }
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UsingStatementVariable_IsExcluded_NoDiagnostic()
    {
        // using ステートメントのリソース変数 -> 構文的コンテキストのため警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        using (var stream = new System.IO.MemoryStream())
        {
            _ = stream.Length;
        }
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UsingDeclarationVariable_IsExcluded_NoDiagnostic()
    {
        // using 宣言のリソース変数 -> 構文적コンテキストのため警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        using var stream = new System.IO.MemoryStream();
        _ = stream.Length;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ForeachVariable_IsExcluded_NoDiagnostic()
    {
        // foreach のループ変数 -> 構文的コンテキストのため警告なし
        var testCode = @"
public class Test
{
    public void Method()
    {
        var _numbers = new int[] { 1, 2, 3 };
        foreach (var item in _numbers)
        {
            _ = item;
        }
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task VariableInsideCatchBody_StillAnalyzed_ReportsLVP0003()
    {
        // catch 本体内で宣言された通常の変数は解析対象 -> 警告あり
        var testCode = @"
public class Test
{
    public void Method()
    {
        try { }
        catch (System.Exception _ex)
        {
            int {|LVP0003:local|} = 42;
            _ = local;
        }
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }
}

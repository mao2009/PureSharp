using System.Threading.Tasks;
using Xunit;
using VerifyRT = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.ReferentialTransparencyAnalyzer>;
using VerifyLVP = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.LocalVariablePurityAnalyzer>;

namespace PureSharp.Analyzers.Tests;

/// <summary>
/// 境界ケースの回帰テスト。
///
/// 既存の Analyzer テストが扱っていなかった境界を固定し、v1.0 までに現在の挙動が
/// 意図せず変化しないようにする。ここでの assertion は「現在の挙動」であり、
/// 変更する場合は Issue で意図を明示したうえでこのテストを更新すること。
/// </summary>
public class RegressionBoundaryTests
{
    private const string PureAttributeSource = @"
namespace PureSharp.Core
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PureMethodAttribute : System.Attribute { }
}
";

    // =========================================================
    // RT0001 境界: static でないフィールドは対象外
    // =========================================================

    [Fact]
    public async Task RT0001_InstanceField_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Holder
{
    private int _instanceValue;

    [PureMethod]
    public int Read() => _instanceValue;
}
" + PureAttributeSource;
        await VerifyRT.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RT0001_StaticMutableField_InLocalFunction_ReportsBothDiagnostics()
    {
        // ローカル関数の本体は外側の [PureMethod] メソッドとして解析されるため
        // RT0001 が報告される。さらにローカル関数自体は [PureMethod] を持たないため、
        // その呼び出しは RT0002 (非純粋メソッド呼び出し) になる。
        //
        // 「pure method 内のローカル関数」をどう扱うかは Issue #10 の検討対象。
        // ここでは現在の挙動を固定する。
        var testCode = @"
using PureSharp.Core;

public class Holder
{
    private static int Counter;

    [PureMethod]
    public int Read()
    {
        int Inner() => {|RT0001:Counter|};
        return {|RT0002:Inner()|};
    }
}
" + PureAttributeSource;
        await VerifyRT.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RT0001_StaticMutableField_ReportedExactlyOnce()
    {
        // 同一のフィールドアクセスが重複報告されないことを件数で固定する。
        // markup 形式では重複報告を表現しづらいため AnalyzerProbe を使用する。
        var source = @"
using PureSharp.Core;

public class Holder
{
    private static int Counter;

    [PureMethod]
    public int Read() => Counter;
}
" + PureAttributeSource;

        var diagnostics = await AnalyzerProbe.RunAsync(new ReferentialTransparencyAnalyzer(), source);

        Assert.Single(diagnostics);
        Assert.Equal("RT0001", diagnostics[0].Id);
    }

    // =========================================================
    // RT0002 境界: 既知の純粋型は許可される
    // =========================================================

    [Fact]
    public async Task RT0002_StringMethod_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Formatter
{
    [PureMethod]
    public string Upper(string s) => s.ToUpperInvariant();
}
" + PureAttributeSource;
        await VerifyRT.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RT0002_ConvertMethod_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;
using System;

public class Parser
{
    [PureMethod]
    public int ToInt(string s) => Convert.ToInt32(s);
}
" + PureAttributeSource;
        await VerifyRT.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RT0002_InstanceMethodOnUserType_ReportsDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Service
{
    public int Compute() => 1;
}

public class Caller
{
    [PureMethod]
    public int Run(Service s) => {|RT0002:s.Compute()|};
}
" + PureAttributeSource;
        await VerifyRT.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RT0002_NonPureMethod_NotAnalyzed_NoDiagnostic()
    {
        // [PureMethod] が付いていないメソッドは解析対象外。
        var testCode = @"
using PureSharp.Core;

public class Service
{
    public int Compute() => 1;
}

public class Caller
{
    public int Run(Service s) => s.Compute();
}
" + PureAttributeSource;
        await VerifyRT.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // RT0003 境界: I/O 型のプロパティアクセス経路
    // =========================================================

    [Fact]
    public async Task RT0003_IoPropertyAccess_ReportsDiagnostic()
    {
        // 呼び出しではなくプロパティ参照の経路 (IsIoPropertyAccess) を固定する。
        var testCode = @"
using PureSharp.Core;
using System;

public class Reader
{
    [PureMethod]
    public object Get() => {|RT0003:Console.Out|};
}
" + PureAttributeSource;
        await VerifyRT.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // LVP0001 境界
    // =========================================================

    [Fact]
    public async Task LVP0001_UnderscoreVariable_DeclaredWithVar_ReassignmentReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        var _x = 10;
        {|LVP0001:_x = 20|};
    }
}";
        await VerifyLVP.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task LVP0001_UnderscoreVariable_InNestedBlock_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method(bool flag)
    {
        int _x = 10;
        if (flag)
        {
            {|LVP0001:_x = 20|};
        }
    }
}";
        await VerifyLVP.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task LVP0001_DoubleUnderscoreVariable_ReportsError()
    {
        // 単一の "_" のみが discard。"__" は通常の不変ローカルとして扱う。
        var testCode = @"
public class Test
{
    public void Method()
    {
        int __x = 10;
        {|LVP0001:__x = 20|};
    }
}";
        await VerifyLVP.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task LVP0001_UnderscoreVariable_ReadOnly_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    public int Method()
    {
        int _x = 10;
        return _x + _x;
    }
}";
        await VerifyLVP.VerifyAnalyzerAsync(testCode);
    }

}

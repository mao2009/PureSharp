using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.ReferentialTransparencyAnalyzer>;

namespace PureSharp.Analyzers.Tests;

public class ReferentialTransparencyAnalyzerTests
{
    // PureMethodAttribute definition to be appended in each test's source code.
    private const string PureAttributeSource = @"
namespace PureSharp.Core
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PureMethodAttribute : System.Attribute { }
}
";

    // =========================================================
    // 正常系: エラーが出ないケース
    // =========================================================

    [Fact]
    public async Task PureMethod_WithArithmeticOnly_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Calculator
{
    [PureMethod]
    public int Add(int a, int b) => a + b;
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PureMethod_CallingMathSin_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;
using System;

public class MathHelper
{
    [PureMethod]
    public double ComputeSine(double x) => Math.Sin(x);
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PureMethod_CallingAnotherPureMethod_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Calculator
{
    [PureMethod]
    public int Add(int a, int b) => a + b;

    [PureMethod]
    public int AddThree(int a, int b, int c) => Add(Add(a, b), c);
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NonPureMethod_AccessingStaticField_NoDiagnostic()
    {
        var testCode = @"
public class Counter
{
    private static int _count = 0;

    // [PureMethod] アトリビュートがない -> アナライザーは無視する
    public int GetAndIncrement() => _count++;
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PureMethod_AccessingReadonlyStaticField_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Constants
{
    private static readonly int MaxValue = 100;

    [PureMethod]
    public bool IsValid(int value) => value <= MaxValue;
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PureMethod_AccessingConstField_NoDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Constants
{
    private const int MaxValue = 100;

    [PureMethod]
    public bool IsValid(int value) => value <= MaxValue;
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // 異常系: RT0001 - 静的フィールドアクセス
    // =========================================================

    [Fact]
    public async Task PureMethod_AccessingStaticMutableField_ReportsDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Counter
{
    private static int _count = 0;

    [PureMethod]
    public int GetCount() => {|RT0001:_count|};
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PureMethod_WritingToStaticField_ReportsDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Counter
{
    private static int _count = 0;

    [PureMethod]
    public void Increment() { {|RT0001:_count|}++; }
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // 異常系: RT0002 - 非純粋メソッド呼び出し
    // =========================================================

    [Fact]
    public async Task PureMethod_CallingNonPureMethod_ReportsDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;

public class Service
{
    public int GetExternalValue() => 42; // [PureMethod] なし

    [PureMethod]
    public int Compute() => {|RT0002:GetExternalValue()|};
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // 異常系: RT0003 - I/O 操作
    // =========================================================

    [Fact]
    public async Task PureMethod_CallingConsoleWriteLine_ReportsDiagnostic()
    {
        var testCode = @"
using PureSharp.Core;
using System;

public class Printer
{
    [PureMethod]
    public void PrintValue(int x) { {|RT0003:Console.WriteLine(x)|}; }
}
" + PureAttributeSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }
}

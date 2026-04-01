using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.FluentIfAnalyzer>;

namespace PureSharp.Analyzers.Tests;

public class FluentIfAnalyzerTests
{
    // FluentIf API の定義を各テストのソースに追記します。
    // using 句は使わず、完全修飾名を使用することで testCode への追記時に CS1529 を回避する
    private const string FluentApiSource = @"
namespace PureSharp.Core
{
    public static class Fluent
    {
        public static ConditionResult<T> If<T>(bool condition, System.Func<T> func)
        {
            if (condition) return new ConditionResult<T>(true, func());
            return new ConditionResult<T>(false, default);
        }

        public static ConditionAction If(bool condition, System.Action action)
        {
            if (condition) action();
            return new ConditionAction(condition);
        }
    }

    public class ConditionResult<T>
    {
        private readonly bool _isResolved;
        private readonly T? _value;

        public ConditionResult(bool isResolved, T? value) { _isResolved = isResolved; _value = value; }

        public ConditionResult<T> ElseIf(bool condition, System.Func<T> func)
        {
            if (_isResolved) return this;
            return condition ? new ConditionResult<T>(true, func()) : this;
        }

        public T Else(System.Func<T> func) => _isResolved ? _value! : func();
        public T Else(T elseValue) => _isResolved ? _value! : elseValue;
    }

    public class ConditionAction
    {
        private readonly bool _isResolved;

        public ConditionAction(bool isResolved) { _isResolved = isResolved; }

        public ConditionAction ElseIf(bool condition, System.Action action)
        {
            if (_isResolved) return this;
            if (condition) action();
            return new ConditionAction(condition);
        }

        public void Else(System.Action action) { if (!_isResolved) action(); }
    }
}
";

    // =========================================================
    // 正常系: エラーが出ないケース
    // =========================================================

    [Fact]
    public async Task ConditionResult_ChainEndsWithElseFunc_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public int Run() =>
        Fluent.If(true, () => 1).Else(() => 0);
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionResult_ChainEndsWithElseValue_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public int Run() =>
        Fluent.If(true, () => 1).Else(0);
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionResult_ElseIfChainEndsWithElse_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public int Run() =>
        Fluent.If(false, () => 1)
              .ElseIf(false, () => 2)
              .Else(() => 3);
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionAction_ChainEndsWithElse_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public void Run() =>
        Fluent.If(true, () => { }).Else(() => { });
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionAction_ElseIfChainEndsWithElse_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public void Run() =>
        Fluent.If(false, () => { })
              .ElseIf(false, () => { })
              .Else(() => { });
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // 異常系: FIF0001 が報告されるべきケース
    // =========================================================

    [Fact]
    public async Task ConditionResult_IfOnly_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public void Run()
    {
        {|FIF0001:Fluent.If(true, () => 1)|};
    }
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionResult_IfElseIfOnly_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public void Run()
    {
        {|FIF0001:Fluent.If(false, () => 1).ElseIf(true, () => 2)|};
    }
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionAction_IfOnly_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public void Run()
    {
        {|FIF0001:Fluent.If(true, () => { })|};
    }
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionAction_IfElseIfOnly_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public void Run()
    {
        {|FIF0001:Fluent.If(false, () => { }).ElseIf(true, () => { })|};
    }
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConditionResult_AssignedWithoutElse_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;

public class Test
{
    public void Run()
    {
        var chain = {|FIF0001:Fluent.If(true, () => 1)|};
    }
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }
}

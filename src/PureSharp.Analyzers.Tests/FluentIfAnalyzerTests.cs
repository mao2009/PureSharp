using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.FluentIfAnalyzer>;

namespace PureSharp.Analyzers.Tests;

public class FluentIfAnalyzerTests
{
    // FluentIf API 縺ｮ螳夂ｾｩ・亥推繝・せ繝医・繧ｽ繝ｼ繧ｹ縺ｫ霑ｽ險假ｼ・
    // using 蜿･縺ｯ菴ｿ繧上★縲∝ｮ悟・菫ｮ鬟ｾ蜷阪ｒ菴ｿ逕ｨ縺吶ｋ縺薙→縺ｧ testCode 縺ｸ縺ｮ霑ｽ險俶凾縺ｫ CS1529 繧貞屓驕ｿ縺吶ｋ
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
    // 豁｣蟶ｸ邉ｻ: 繧ｨ繝ｩ繝ｼ縺悟・縺ｪ縺・∋縺阪こ繝ｼ繧ｹ
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
    // 逡ｰ蟶ｸ邉ｻ: FIF0001 縺悟ｱ蜻翫＆繧後ｋ縺ｹ縺阪こ繝ｼ繧ｹ
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

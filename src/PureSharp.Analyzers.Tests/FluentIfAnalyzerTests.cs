using System.Threading.Tasks;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.FluentIfAnalyzer>;

namespace PureSharp.Analyzers.Tests;

public class FluentIfAnalyzerTests
{
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

    [Fact]
    public async Task ConditionResult_ChainEndsWithElseFunc_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;
public class Test
{
    public int Run() => Fluent.If(true, () => 1).Else(() => 0);
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
    public int Run() => Fluent.If(true, () => 1).Else(0);
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ResultConsumedAfterElse_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;
public class Test
{
    public string Run() => Fluent.If(true, () => 1).Else(0).ToString();
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
    public void Run() => Fluent.If(true, () => { }).Else(() => { });
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

    [Fact]
    public async Task ParenthesizedChain_EndsWithElse_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;
public class Test
{
    public int Run() => ((Fluent.If(false, () => 1))).Else(2);
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NestedChains_BothTerminate_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;
public class Test
{
    public int Run() =>
        Fluent.If(true, () => Fluent.If(false, () => 1).Else(2))
              .Else(0);
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task GenericInference_ExplicitTypeArgument_NoDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;
public class Test
{
    public string Run() => Fluent.If<string>(true, () => ""yes"").Else(""no"");
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

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

    [Fact]
    public async Task UnrelatedTerminalMethod_DoesNotCountAsElse_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;
public class Test
{
    public void Run()
    {
        _ = {|FIF0001:Fluent.If(true, () => 1).ToString()|};
    }
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task LambdaContainingIncompleteChain_ReportsDiagnostic()
    {
        var testCode = @"
using System;
using PureSharp.Core;
public class Test
{
    public Func<object> Make() => () => {|FIF0001:Fluent.If(true, () => 1)|};
}
" + FluentApiSource;
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }
}

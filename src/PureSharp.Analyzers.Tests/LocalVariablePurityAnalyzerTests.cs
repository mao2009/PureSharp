using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.LocalVariablePurityAnalyzer>;

namespace PureSharp.Analyzers.Tests;

public class LocalVariablePurityAnalyzerTests
{
    // =========================================================
    // 正常系: エラーが�EなぁE��きケース
    // =========================================================

    [Fact]
    public async Task Initialized_UnderscoreVariable_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 10;
        int y = _x + 1;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Discard_Symbol_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        _ = System.Guid.NewGuid();
        _ = 1 + 1;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NormalVariable_Reassignment_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int x = 10;
        x = 20;
        x += 5;
        x++;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ClassField_Reassignment_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    private int _field = 0;

    public void Method()
    {
        _field = 10;
        _field++;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // 異常系: LVP0001 - 再代入禁止
    // =========================================================

    [Fact]
    public async Task UnderscoreVariable_SimpleAssignment_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 10;
        {|LVP0001:_x = 20|};
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UnderscoreVariable_CompoundAssignment_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 10;
        {|LVP0001:_x += 5|};
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UnderscoreVariable_Increment_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 10;
        {|LVP0001:_x++|};
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    // =========================================================
    // 異常系: LVP0002 - 宣言時�E期化強制
    // =========================================================

    [Fact]
    public async Task UnderscoreVariable_NoInitializer_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int {|LVP0002:_x|};
        {|LVP0001:_x = 10|};
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UnderscoreVariable_MultiDeclaration_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 10, {|LVP0002:_y|};
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }
}

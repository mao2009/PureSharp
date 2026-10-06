using System.Threading.Tasks;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    PureSharp.Analyzers.LocalVariablePurityAnalyzer>;

namespace PureSharp.Analyzers.Tests;

public class LocalVariablePurityAnalyzerTests
{
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

    [Fact]
    public async Task UnderscoreVariable_InArgument_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    private static int Read(in int value) => value;

    public int Method()
    {
        int _x = 10;
        return Read(in _x);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ForeachUnderscoreVariable_ImplicitInitialization_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    public void Method(int[] values)
    {
        foreach (var _value in values)
            System.Console.WriteLine(_value);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PatternVariable_IsNotDeclarationTimeImmutableLocal_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    public int Method(object value)
    {
        if (value is int _number)
            return _number;
        return 0;
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task TupleDeconstructionDeclaration_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    private static (int, int) F() => (1, 2);

    public void Method()
    {
        var (_a, _b) = F();
        System.Console.WriteLine(_a + _b);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task TupleDeconstructionDeclaration_WithDiscard_NoDiagnostic()
    {
        var testCode = @"
public class Test
{
    private static (int, int) F() => (1, 2);

    public void Method()
    {
        var (_, _c) = F();
        System.Console.WriteLine(_c);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

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

    [Fact]
    public async Task UnderscoreVariable_RefArgument_ReportsError()
    {
        var testCode = @"
public class Test
{
    private static void Mutate(ref int value) => value++;

    public void Method()
    {
        int _x = 10;
        Mutate({|LVP0001:ref _x|});
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UnderscoreVariable_OutArgument_ReportsError()
    {
        var testCode = @"
public class Test
{
    private static void Assign(out int value) => value = 42;

    public void Method()
    {
        int _x = 10;
        Assign({|LVP0001:out _x|});
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task UnderscoreVariable_DeconstructionAssignment_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 1;
        int y = 2;
        ({|LVP0001:_x|}, y) = (3, 4);
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CapturedUnderscoreVariable_ReassignedInLambda_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 1;
        System.Action action = () => {|LVP0001:_x = 2|};
        action();
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CapturedUnderscoreVariable_ReassignedInLocalFunction_ReportsError()
    {
        var testCode = @"
public class Test
{
    public void Method()
    {
        int _x = 1;
        void Mutate() => {|LVP0001:_x = 2|};
        Mutate();
    }
}";
        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

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

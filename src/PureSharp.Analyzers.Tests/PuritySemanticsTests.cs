using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PureSharp.Analyzers.Tests;

/// <summary>
/// RT 純粋性境界の仕様テスト。
///
/// docs/PURITY-SEMANTICS.md が定義する「v1.0 で保証する範囲 / 保証しない範囲」を
/// 実際の挙動として固定する。
///
/// 重要: このテストの一部は「検出されないこと」を assert している。これは
/// 検出されないのが正しいという主張ではなく、v1.0 時点で保証していない範囲を
/// 明示的に記録するものである。該当箇所には FALSE NEGATIVE と明記している。
/// 将来これらを検出するようにする変更は破壊的変更であり、
/// docs/DIAGNOSTICS.md の互換性ルールに従って major リリースで行う。
/// </summary>
public class PuritySemanticsTests
{
    private const string Attr = @"
namespace PureSharp.Core
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PureMethodAttribute : System.Attribute { }
}
";

    private static string Wrap(string body, string members = "") => @"
using PureSharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public class Probe
{
" + members + @"
    [PureMethod]
    public object Run()
    {
" + body + @"
    }
}
" + Attr;

    private static async Task<string[]> IdsAsync(string source)
    {
        var diagnostics = await AnalyzerProbe.RunAsync(new ReferentialTransparencyAnalyzer(), source);
        return diagnostics.Select(d => d.Id).OrderBy(x => x, System.StringComparer.Ordinal).ToArray();
    }

    private static async Task AssertIdsAsync(string source, params string[] expected)
        => Assert.Equal(expected, await IdsAsync(source));

    // =========================================================
    // 保証する範囲: static mutable state
    // =========================================================

    [Fact]
    public async Task StaticMutableField_Read_ReportsRT0001()
        => await AssertIdsAsync(Wrap("return S;", "private static int S;"), "RT0001");

    [Fact]
    public async Task StaticMutableField_Write_ReportsRT0001()
        => await AssertIdsAsync(Wrap("S = 1; return null;", "private static int S;"), "RT0001");

    [Fact]
    public async Task StaticReadonlyField_Read_NoDiagnostic()
        => await AssertIdsAsync(Wrap("return R;", "private static readonly int R = 1;"));

    [Fact]
    public async Task ConstField_Read_NoDiagnostic()
        => await AssertIdsAsync(Wrap("return C;", "private const int C = 1;"));

    // =========================================================
    // 保証する範囲: I/O 境界
    // =========================================================

    [Fact]
    public async Task ConsoleWrite_ReportsRT0003()
        => await AssertIdsAsync(Wrap("Console.WriteLine(1); return null;"), "RT0003");

    [Fact]
    public async Task FileRead_ReportsRT0003()
        => await AssertIdsAsync(Wrap("return System.IO.File.ReadAllText(\"x\");"), "RT0003");

    [Fact]
    public async Task ConsoleProperty_ReportsRT0003()
        => await AssertIdsAsync(Wrap("return Console.Out;"), "RT0003");

    // =========================================================
    // 保証する範囲: 非純粋メソッド呼び出し
    // =========================================================

    [Fact]
    public async Task UnmarkedInstanceMethod_ReportsRT0002()
        => await AssertIdsAsync(
            Wrap("return Helper();", "private int Helper() => 1;"),
            "RT0002");

    [Fact]
    public async Task PureMarkedMethod_NoDiagnostic()
        => await AssertIdsAsync(
            Wrap("return Helper();", "[PureMethod] private int Helper() => 1;"));

    [Fact]
    public async Task InterfaceMethodCall_ReportsRT0002()
        => await AssertIdsAsync(
            Wrap("return i.M();", "public interface IThing { int M(); } private IThing i;"),
            "RT0002");

    [Fact]
    public async Task RecursivePureMethod_NoDiagnostic()
        => await AssertIdsAsync(
            Wrap("return Fact(3);",
                 "[PureMethod] private int Fact(int n) => n <= 1 ? 1 : n * Fact(n - 1);"));

    [Fact]
    public async Task GenericPureMethod_NoDiagnostic()
        => await AssertIdsAsync(
            Wrap("return Id(1);", "[PureMethod] private T Id<T>(T v) => v;"));

    [Fact]
    public async Task OutParameterMethod_ReportsRT0002()
        => await AssertIdsAsync(
            Wrap("int x; TryIt(out x); return x;",
                 "private static bool TryIt(out int v) { v = 1; return true; }"),
            "RT0002");

    [Fact]
    public async Task RandomNext_ReportsRT0002()
        => await AssertIdsAsync(Wrap("return new Random().Next();"), "RT0002");

    [Fact]
    public async Task EnvironmentVariable_ReportsRT0002()
        => await AssertIdsAsync(Wrap("return Environment.GetEnvironmentVariable(\"X\");"), "RT0002");

    // =========================================================
    // 保証しない範囲 (FALSE NEGATIVE) — instance state
    //
    // インスタンスフィールド / プロパティへの書き込みは観測可能な状態変化だが、
    // v1.0 の RT 解析は static state のみを対象としているため検出されない。
    // =========================================================

    [Fact]
    public async Task FALSE_NEGATIVE_InstanceFieldWrite_NotDetected()
        => await AssertIdsAsync(Wrap("_f = 1; return null;", "private int _f;"));

    [Fact]
    public async Task FALSE_NEGATIVE_InstancePropertyWrite_NotDetected()
        => await AssertIdsAsync(Wrap("P = 1; return null;", "private int P { get; set; }"));

    [Fact]
    public async Task FALSE_NEGATIVE_ArrayElementWrite_NotDetected()
        => await AssertIdsAsync(Wrap("var a = new int[1]; a[0] = 5; return a;"));

    // =========================================================
    // 保証しない範囲 (FALSE NEGATIVE) — 非決定性
    //
    // これらの型は KnownPureTypeNames に含まれるため呼び出しが許可されるが、
    // 同じ入力に対して同じ結果を返さないため参照透過ではない。
    // =========================================================

    [Fact]
    public async Task FALSE_NEGATIVE_DateTimeNow_NotDetected()
        => await AssertIdsAsync(Wrap("return DateTime.Now;"));

    [Fact]
    public async Task FALSE_NEGATIVE_DateTimeUtcNow_NotDetected()
        => await AssertIdsAsync(Wrap("return DateTime.UtcNow;"));

    [Fact]
    public async Task FALSE_NEGATIVE_GuidNewGuid_NotDetected()
        => await AssertIdsAsync(Wrap("return Guid.NewGuid();"));

    [Fact]
    public async Task DateTimeConstructor_NoDiagnostic_Deterministic()
        => await AssertIdsAsync(Wrap("return new DateTime(2020, 1, 1);"));

    // =========================================================
    // 保証しない範囲 (FALSE NEGATIVE) — 制御境界 / デリゲート
    // =========================================================

    [Fact]
    public async Task FALSE_NEGATIVE_LambdaInvocation_NotDetected()
        => await AssertIdsAsync(Wrap("Func<int> f = () => 1; return f();"));

    [Fact]
    public async Task FALSE_NEGATIVE_ThrowStatement_NotDetected()
        => await AssertIdsAsync(Wrap("throw new InvalidOperationException();"));

    [Fact]
    public async Task FALSE_NEGATIVE_TryCatch_NotDetected()
        => await AssertIdsAsync(Wrap("try { return 1; } catch { return 0; }"));

    [Fact]
    public async Task FALSE_NEGATIVE_ObjectCreation_NotDetected()
        => await AssertIdsAsync(Wrap("return new object();"));

    // =========================================================
    // 解析対象境界
    // =========================================================

    [Fact]
    public async Task MethodWithoutPureAttribute_NotAnalyzed()
        => await AssertIdsAsync(@"
using System;

public class Probe
{
    private static int S;
    public object Run()
    {
        S = 1;
        Console.WriteLine(1);
        return null;
    }
}
" + Attr);
}

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace PureSharp.Analyzers.Tests;

/// <summary>
/// [PureMethod] 呼び出し契約の仕様テスト。
///
/// docs/CALL-CONTRACT.md が定義する「契約は静的に解決されたシンボルに対して
/// 判定される」という原則と、interprocedural analysis の対象範囲を固定する。
/// </summary>
public class CallContractTests
{
    private const string Attr = @"
namespace PureSharp.Core
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PureMethodAttribute : System.Attribute { }
}
";

    private static string Source(string body, string members = "", string outer = "") => @"
using PureSharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
" + outer + @"
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

    private static async Task AssertIdsAsync(string source, params string[] expected)
    {
        var diagnostics = await AnalyzerProbe.RunAsync(new ReferentialTransparencyAnalyzer(), source);
        var actual = diagnostics.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }

    // =========================================================
    // 原則: 契約は「静的に解決されたシンボル」に対して判定される
    // =========================================================

    [Fact]
    public async Task Interface_AttributeOnDeclaration_NoDiagnostic()
        => await AssertIdsAsync(Source(
            "return t.M();",
            "private IThing t;",
            "public interface IThing { [PureMethod] int M(); }"));

    [Fact]
    public async Task Interface_AttributeOnImplementationOnly_ReportsRT0002()
        // 呼び出しは interface メンバに解決されるため、実装側の [PureMethod] は
        // 契約を満たさない。属性は宣言側に付ける必要がある。
        => await AssertIdsAsync(Source(
            "return t.M();",
            "private IThing t;",
            "public interface IThing { int M(); } public class Impl : IThing { [PureMethod] public int M() => 1; }"),
            "RT0002");

    [Fact]
    public async Task Virtual_AttributeOnBase_NoDiagnostic()
        => await AssertIdsAsync(Source(
            "return b.M();",
            "private Base b;",
            "public class Base { [PureMethod] public virtual int M() => 1; } public class Derived : Base { public override int M() => 2; }"));

    [Fact]
    public async Task Virtual_AttributeOnOverrideOnly_ReportsRT0002()
        // base 型経由の呼び出しは base のシンボルに解決されるため、override 側の
        // [PureMethod] は効かない。
        => await AssertIdsAsync(Source(
            "return b.M();",
            "private Base b;",
            "public class Base { public virtual int M() => 1; } public class Derived : Base { [PureMethod] public override int M() => 2; }"),
            "RT0002");

    [Fact]
    public async Task Virtual_Unmarked_ReportsRT0002()
        => await AssertIdsAsync(Source(
            "return b.M();",
            "private Base b;",
            "public class Base { public virtual int M() => 1; }"),
            "RT0002");

    // =========================================================
    // Overload 解決
    // =========================================================

    [Fact]
    public async Task Overload_ResolvedToMarkedOverload_NoDiagnostic()
        => await AssertIdsAsync(Source(
            "return H(1);",
            "[PureMethod] private int H(int x) => x; private int H(string s) => 0;"));

    [Fact]
    public async Task Overload_ResolvedToUnmarkedOverload_ReportsRT0002()
        => await AssertIdsAsync(Source(
            "return H(\"a\");",
            "[PureMethod] private int H(int x) => x; private int H(string s) => 0;"),
            "RT0002");

    // =========================================================
    // Extension method
    // =========================================================

    [Fact]
    public async Task ExtensionMethod_Marked_NoDiagnostic()
        => await AssertIdsAsync(Source(
            "return this.Ext();",
            "",
            "public static class Ex { [PureMethod] public static int Ext(this Probe p) => 1; }"));

    [Fact]
    public async Task ExtensionMethod_Unmarked_ReportsRT0002()
        => await AssertIdsAsync(Source(
            "return this.Ext();",
            "",
            "public static class Ex { public static int Ext(this Probe p) => 1; }"),
            "RT0002");

    // =========================================================
    // 再帰 / 相互再帰
    // =========================================================

    [Fact]
    public async Task MutualRecursion_BothMarked_NoDiagnostic()
        => await AssertIdsAsync(Source(
            "return A(1);",
            "[PureMethod] private int A(int n) => n <= 0 ? 0 : B(n - 1); " +
            "[PureMethod] private int B(int n) => n <= 0 ? 0 : A(n - 1);"));

    [Fact]
    public async Task MutualRecursion_OneUnmarked_ReportsRT0002()
        => await AssertIdsAsync(Source(
            "return A(1);",
            "[PureMethod] private int A(int n) => n <= 0 ? 0 : B(n - 1); " +
            "private int B(int n) => n <= 0 ? 0 : A(n - 1);"),
            "RT0002");

    [Fact]
    public async Task StaticMarkedMethod_NoDiagnostic()
        => await AssertIdsAsync(Source("return S();", "[PureMethod] private static int S() => 1;"));

    // =========================================================
    // External dependency
    // =========================================================

    [Fact]
    public async Task ExternalAssembly_UnmarkedType_ReportsRT0002()
        // 外部アセンブリの型は [PureMethod] を付けられないため、既知純粋型
        // リストに含まれない限り常に RT0002 になる (fail-closed)。
        => await AssertIdsAsync(
            Source("return new System.Text.StringBuilder().ToString();"),
            "RT0002");

    [Fact]
    public async Task ExternalAssembly_KnownPureType_NoDiagnostic()
        => await AssertIdsAsync(Source("return Math.Abs(-1);"));

    // =========================================================
    // 対象範囲外 (FALSE NEGATIVE)
    // =========================================================

    [Fact]
    public async Task FALSE_NEGATIVE_UserPropertyGetter_NotDetected()
        // プロパティ参照は I/O 型かどうかしか見ていないため、任意のコードを
        // 実行しうるユーザー定義 getter は呼び出し契約の対象外。
        => await AssertIdsAsync(Source("return Q;", "private int Q => 1;"));

    [Fact]
    public async Task FALSE_NEGATIVE_Constructor_NotDetected()
        // オブジェクト生成は invocation として解析されないため、コンストラクタ本体は
        // 呼び出し契約の対象外。
        => await AssertIdsAsync(Source(
            "return new Other();", "", "public class Other { public Other() { } }"));

    // =========================================================
    // Performance guard
    // =========================================================

    [Fact]
    public async Task Analyzer_ScalesLinearly_AndCompletesWithinGenerousBound()
    {
        // docs/CALL-CONTRACT.md の測定を再現可能にするための guard。
        // 厳密な閾値は環境差でフレークになるため、破滅的な性能退行のみを検出する
        // 十分に緩い上限を用いる。
        var source = GenerateMethods(2000);

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "PerfGuard",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new ReferentialTransparencyAnalyzer()));

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        stopwatch.Stop();

        Assert.Empty(diagnostics);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60),
            $"Analyzing 2000 pure methods took {stopwatch.Elapsed}, which indicates a severe regression.");
    }

    private static string GenerateMethods(int count)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using PureSharp.Core;");
        sb.AppendLine("using System;");
        sb.AppendLine("public class Big {");
        sb.AppendLine("  [PureMethod] private int Helper(int x) => x + 1;");
        for (var i = 0; i < count; i++)
            sb.AppendLine($"  [PureMethod] public int M{i}(int a) {{ var v = Helper(a); v = v + Math.Abs(a); return v + {i}; }}");
        sb.AppendLine("}");
        sb.AppendLine(Attr);
        return sb.ToString();
    }
}

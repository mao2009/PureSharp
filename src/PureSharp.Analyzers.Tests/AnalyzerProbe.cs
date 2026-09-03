using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PureSharp.Analyzers.Tests;

/// <summary>
/// Analyzer を直接実行して報告された Diagnostic をそのまま取得するヘルパー。
///
/// Microsoft.CodeAnalysis.Testing の markup 形式では「同一位置に同じ Diagnostic が
/// 複数回報告される」ようなケースを表現しづらいため、件数と位置を直接検証したい
/// 境界テストではこちらを使用する。
/// </summary>
internal static class AnalyzerProbe
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Enumerable).Assembly,
        };

        var refs = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            foreach (var name in assembly.GetReferencedAssemblies().Select(a => a.Name).Concat(new[] { assembly.GetName().Name }))
            {
                if (name is null || !seen.Add(name)) continue;
                try
                {
                    var loaded = System.Reflection.Assembly.Load(name);
                    if (!string.IsNullOrEmpty(loaded.Location))
                        refs.Add(MetadataReference.CreateFromFile(loaded.Location));
                }
                catch
                {
                    // 参照できないアセンブリは無視する（判定に必要な型は corelib に含まれる）。
                }
            }
        }

        return refs.ToImmutableArray();
    }

    /// <summary>指定ソースに対して analyzer を実行し、報告された Diagnostic を返します。</summary>
    public static async Task<ImmutableArray<Diagnostic>> RunAsync(DiagnosticAnalyzer analyzer, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "Probe",
            new[] { tree },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create(analyzer));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}

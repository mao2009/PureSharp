using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PureSharp.Analyzers;

/// <summary>
/// 実質的に不変なローカル変数（再代入なし）がアンダースコア命名規則に従っているか検証する Roslyn アナライザー。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ImmutableNamingSuggestionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>LVP0003: 実質的に不変なローカル変数のアンダースコア命名規則の適用推奨</summary>
    public static readonly DiagnosticDescriptor LVP0003 = new(
        id: "LVP0003",
        title: "不変ローカル変数への命名規則適用の提案",
        messageFormat: "ローカル変数 '{0}' は実質的に不変です。アンダースコア '_' で 始まる名前にすることを検討してください",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "一度も再代入されないローカル変数はアンダースコア (_) で始まる名前にすることで、不変性を明示できます.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(LVP0003);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationBlockAction(AnalyzeBlock);
    }

    private static void AnalyzeBlock(OperationBlockAnalysisContext context)
    {
        // フェーズ 1: ブロック内のローカル変数宣言を収集し、代入カウントを 0 で初期化
        var reassignmentCounts = new Dictionary<ILocalSymbol, int>(SymbolEqualityComparer.Default);
        var declarationLocations = new Dictionary<ILocalSymbol, Location>(SymbolEqualityComparer.Default);

        foreach (var block in context.OperationBlocks)
        {
            foreach (var op in block.DescendantsAndSelf())
            {
                if (op is IVariableDeclaratorOperation declarator &&
                    declarator.Symbol.Kind == SymbolKind.Local)
                {
                    var symbol = declarator.Symbol;
                    if (!reassignmentCounts.ContainsKey(symbol))
                    {
                        reassignmentCounts[symbol] = 0;
                        // 変数名トークンの位置を使用（宣言全体ではなく識別子のみ）
                        declarationLocations[symbol] = symbol.Locations.Length > 0
                            ? symbol.Locations[0]
                            : declarator.Syntax.GetLocation();
                    }
                }
            }
        }

        // フェーズ 2: 構文的コンテキスト（catch/using/foreach）を除外リストに収集
        var excludedSymbols = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);

        foreach (var block in context.OperationBlocks)
        {
            foreach (var op in block.DescendantsAndSelf())
            {
                switch (op)
                {
                    case ICatchClauseOperation catchOp:
                        CollectLocalSymbols(catchOp.ExceptionDeclarationOrExpression, excludedSymbols);
                        break;
                    case IUsingOperation usingOp:
                        CollectLocalSymbols(usingOp.Resources, excludedSymbols);
                        break;
                    case IUsingDeclarationOperation usingDeclOp:
                        CollectLocalSymbols(usingDeclOp.DeclarationGroup, excludedSymbols);
                        break;
                    case IForEachLoopOperation forEachOp:
                        CollectLocalSymbols(forEachOp.LoopControlVariable, excludedSymbols);
                        break;
                }
            }
        }

        // フェーズ 3: ブロック内の再代入操作をカウント
        foreach (var block in context.OperationBlocks)
        {
            foreach (var op in block.DescendantsAndSelf())
            {
                var target = op switch
                {
                    ISimpleAssignmentOperation { Target: ILocalReferenceOperation lr } => lr.Local,
                    ICompoundAssignmentOperation { Target: ILocalReferenceOperation lr } => lr.Local,
                    ICoalesceAssignmentOperation { Target: ILocalReferenceOperation lr } => lr.Local,
                    IIncrementOrDecrementOperation { Target: ILocalReferenceOperation lr } => lr.Local,
                    _ => null
                };

                if (target is not null && reassignmentCounts.ContainsKey(target))
                    reassignmentCounts[target]++;

                // ref / out 引数としての受け渡しも書き込みとして扱う
                if (op is IArgumentOperation { Value: ILocalReferenceOperation argLocal } arg &&
                    (arg.Parameter?.RefKind == RefKind.Ref || arg.Parameter?.RefKind == RefKind.Out) &&
                    reassignmentCounts.ContainsKey(argLocal.Local))
                {
                    reassignmentCounts[argLocal.Local]++;
                }
            }
        }

        // フェーズ 4: 再代入ゼロかつ除外リストになくアンダースコアなしの変数に LVP0003 を報告
        foreach (var kvp in reassignmentCounts)
        {
            var symbol = kvp.Key;
            var count = kvp.Value;
            Location location;

            if (count == 0 &&
                !excludedSymbols.Contains(symbol) &&
                symbol.Name != "_" &&
                !symbol.Name.StartsWith("_") &&
                declarationLocations.TryGetValue(symbol, out location))
            {
                context.ReportDiagnostic(Diagnostic.Create(LVP0003, location, symbol.Name));
            }
        }
    }

    private static void CollectLocalSymbols(IOperation? op, HashSet<ILocalSymbol> symbols)
    {
        if (op == null) return;
        foreach (var descendant in op.DescendantsAndSelf())
        {
            if (descendant is IVariableDeclaratorOperation declarator &&
                declarator.Symbol.Kind == SymbolKind.Local)
            {
                symbols.Add(declarator.Symbol);
            }
        }
    }
}

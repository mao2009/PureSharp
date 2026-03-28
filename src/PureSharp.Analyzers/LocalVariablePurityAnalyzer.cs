using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PureSharp.Analyzers;

/// <summary>
/// アンダースコア（_）で始まるローカル変数の不変性を強制する Roslyn アナライザー。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LocalVariablePurityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>LVP0001: 不変ローカル変数への再代入</summary>
    public static readonly DiagnosticDescriptor LVP0001 = new(
        id: "LVP0001",
        title: "不変ローカル変数への再代入禁止",
        messageFormat: "不変ローカル変数 '{0}' に値を再代入することはできません",
        category: "Purity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "アンダースコア (_) で始まるローカル変数は不変として扱われ、宣言後の再代入は禁止されています。");

    /// <summary>LVP0002: 不変ローカル変数の宣言時初期化の強制</summary>
    public static readonly DiagnosticDescriptor LVP0002 = new(
        id: "LVP0002",
        title: "不変ローカル変数の宣言時初期化強制",
        messageFormat: "不変ローカル変数 '{0}' は宣言時に初期化される必要があります",
        category: "Purity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "アンダースコア (_) で始まるローカル変数は不変として扱われ、宣言と同時に初期値を代入する必要があります。");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(LVP0001, LVP0002);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeVariableDeclarator, OperationKind.VariableDeclarator);
        context.RegisterOperationAction(AnalyzeSimpleAssignment, OperationKind.SimpleAssignment);
        context.RegisterOperationAction(AnalyzeCompoundAssignment, OperationKind.CompoundAssignment);
        context.RegisterOperationAction(AnalyzeIncrement, OperationKind.Increment);
        context.RegisterOperationAction(AnalyzeDecrement, OperationKind.Decrement);
    }

    private static void AnalyzeVariableDeclarator(OperationAnalysisContext context)
    {
        var op = (IVariableDeclaratorOperation)context.Operation;
        if (PurityRulesEngine.IsMissingInitializer(op))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                LVP0002,
                op.Syntax.GetLocation(),
                op.Symbol.Name));
        }
    }

    private static void AnalyzeSimpleAssignment(OperationAnalysisContext context)
    {
        ReportIfReassignment(context, context.Operation);
    }

    private static void AnalyzeCompoundAssignment(OperationAnalysisContext context)
    {
        ReportIfReassignment(context, context.Operation);
    }

    private static void AnalyzeIncrement(OperationAnalysisContext context)
    {
        ReportIfReassignment(context, context.Operation);
    }

    private static void AnalyzeDecrement(OperationAnalysisContext context)
    {
        ReportIfReassignment(context, context.Operation);
    }

    private static void ReportIfReassignment(OperationAnalysisContext context, IOperation op)
    {
        if (PurityRulesEngine.IsReassignmentToPureLocal(op))
        {
            // 代入先（Target）のシンボル名を取得
            string localName = "unknown";
            if (op is IAssignmentOperation assignment && assignment.Target is ILocalReferenceOperation lr1)
                localName = lr1.Local.Name;
            else if (op is ICompoundAssignmentOperation compound && compound.Target is ILocalReferenceOperation lr2)
                localName = lr2.Local.Name;
            else if (op is IIncrementOrDecrementOperation incDec && incDec.Target is ILocalReferenceOperation lr3)
                localName = lr3.Local.Name;

            context.ReportDiagnostic(Diagnostic.Create(
                LVP0001,
                op.Syntax.GetLocation(),
                localName));
        }
    }
}

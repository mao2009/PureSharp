using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using PureSharp.Core;
using PureSharp.Core.Resources;

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
        title: new LocalizableResourceString(nameof(DiagnosticResources.LVP0001_Title), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        messageFormat: new LocalizableResourceString(nameof(DiagnosticResources.LVP0001_MessageFormat), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        category: "Purity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(DiagnosticResources.LVP0001_Description), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)));

    /// <summary>LVP0002: 不変ローカル変数の宣言時初期化の強制</summary>
    public static readonly DiagnosticDescriptor LVP0002 = new(
        id: "LVP0002",
        title: new LocalizableResourceString(nameof(DiagnosticResources.LVP0002_Title), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        messageFormat: new LocalizableResourceString(nameof(DiagnosticResources.LVP0002_MessageFormat), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        category: "Purity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(DiagnosticResources.LVP0002_Description), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)));

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
        context.RegisterOperationAction(AnalyzeArgument, OperationKind.Argument);
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

    private static void AnalyzeArgument(OperationAnalysisContext context)
    {
        var argument = (IArgumentOperation)context.Operation;
        if (argument.Parameter?.RefKind is not (RefKind.Ref or RefKind.Out))
            return;

        if (argument.Value is not ILocalReferenceOperation localReference ||
            !PurityRulesEngine.IsPureLocalVariable(localReference.Local))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            LVP0001,
            argument.Syntax.GetLocation(),
            localReference.Local.Name));
    }

    private static void ReportIfReassignment(OperationAnalysisContext context, IOperation op)
    {
        if (PurityRulesEngine.IsReassignmentToPureLocal(op))
        {
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

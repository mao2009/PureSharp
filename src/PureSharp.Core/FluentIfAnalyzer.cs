using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PureSharp.Core;

namespace PureSharp.Analyzers;

/// <summary>
/// <c>Fluent.If</c> で始まるメソッドチェーンが必ず <c>.Else(...)</c> で終了することを検証するアナライザー。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FluentIfAnalyzer : DiagnosticAnalyzer
{
    /// <summary>FIF0001: Else の欠落</summary>
    public static readonly DiagnosticDescriptor FIF0001 = new(
        id: "FIF0001",
        title: new LocalizableResourceString(nameof(PureSharp.Core.Resources.DiagnosticResources.FIF0001_Title), PureSharp.Core.Resources.DiagnosticResources.ResourceManager, typeof(PureSharp.Core.Resources.DiagnosticResources)),
        messageFormat: new LocalizableResourceString(nameof(PureSharp.Core.Resources.DiagnosticResources.FIF0001_MessageFormat), PureSharp.Core.Resources.DiagnosticResources.ResourceManager, typeof(PureSharp.Core.Resources.DiagnosticResources)),
        category: "FluentIf",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(PureSharp.Core.Resources.DiagnosticResources.FIF0001_Description), PureSharp.Core.Resources.DiagnosticResources.ResourceManager, typeof(PureSharp.Core.Resources.DiagnosticResources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(FIF0001);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var conditionResultType = compilationContext.Compilation
                .GetTypeByMetadataName("PureSharp.Core.ConditionResult`1");
            var conditionActionType = compilationContext.Compilation
                .GetTypeByMetadataName("PureSharp.Core.ConditionAction");

            if (conditionResultType is null && conditionActionType is null) return;

            compilationContext.RegisterSyntaxNodeAction(
                ctx => CheckInvocation(ctx, conditionResultType, conditionActionType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void CheckInvocation(
        SyntaxNodeAnalysisContext ctx,
        INamedTypeSymbol? conditionResultType,
        INamedTypeSymbol? conditionActionType)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        var typeInfo = ctx.SemanticModel.GetTypeInfo(invocation, ctx.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol namedType) return;

        var isConditionResult = conditionResultType is not null &&
            SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, conditionResultType);

        var isConditionAction = conditionActionType is not null &&
            SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, conditionActionType);

        if (!isConditionResult && !isConditionAction) return;

        // 親ノードが MemberAccessExpression であれば、このチェーンはさらに続いている (非終端)
        if (invocation.Parent is MemberAccessExpressionSyntax) return;

        ctx.ReportDiagnostic(Diagnostic.Create(FIF0001, invocation.GetLocation()));
    }
}

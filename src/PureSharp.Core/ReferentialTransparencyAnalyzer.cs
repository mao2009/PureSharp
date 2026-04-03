using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using PureSharp.Core;
using PureSharp.Core.Resources;

namespace PureSharp.Analyzers;

/// <summary>
/// [PureMethod] アトリビュートが付与されたメソッドの参照透過性を検証する Roslyn アナライザー。
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReferentialTransparencyAnalyzer : DiagnosticAnalyzer
{
    private const string PureMethodAttributeFullName = "PureSharp.Core.PureMethodAttribute";

    /// <summary>RT0001: static かつ非 readonly なフィールドへのアクセス</summary>
    public static readonly DiagnosticDescriptor RT0001 = new(
        id: "RT0001",
        title: new LocalizableResourceString(nameof(DiagnosticResources.RT0001_Title), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        messageFormat: new LocalizableResourceString(nameof(DiagnosticResources.RT0001_MessageFormat), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        category: "Purity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(DiagnosticResources.RT0001_Description), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)));

    /// <summary>RT0002: [PureMethod] でないメソッドの呼び出し</summary>
    public static readonly DiagnosticDescriptor RT0002 = new(
        id: "RT0002",
        title: new LocalizableResourceString(nameof(DiagnosticResources.RT0002_Title), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        messageFormat: new LocalizableResourceString(nameof(DiagnosticResources.RT0002_MessageFormat), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        category: "Purity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(DiagnosticResources.RT0002_Description), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)));

    /// <summary>RT0003: I/O 操作</summary>
    public static readonly DiagnosticDescriptor RT0003 = new(
        id: "RT0003",
        title: new LocalizableResourceString(nameof(DiagnosticResources.RT0003_Title), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        messageFormat: new LocalizableResourceString(nameof(DiagnosticResources.RT0003_MessageFormat), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        category: "Purity",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(DiagnosticResources.RT0003_Description), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(RT0001, RT0002, RT0003);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var pureAttributeType = compilationContext.Compilation
                .GetTypeByMetadataName(PureMethodAttributeFullName);

            if (pureAttributeType is null) return;

            compilationContext.RegisterOperationBlockStartAction(blockContext =>
            {
                if (blockContext.OwningSymbol is not IMethodSymbol method) return;

                if (!HasPureAttribute(method, pureAttributeType)) return;

                // RT0001: static 非 readonly フィールドアクセス
                blockContext.RegisterOperationAction(
                    operationContext =>
                    {
                        var op = (IFieldReferenceOperation)operationContext.Operation;
                        if (PurityRulesEngine.IsStaticMutableFieldAccess(op))
                        {
                            operationContext.ReportDiagnostic(Diagnostic.Create(
                                RT0001,
                                op.Syntax.GetLocation(),
                                method.Name,
                                op.Field.Name));
                        }
                    },
                    OperationKind.FieldReference);

                // RT0003: I/O プロパティアクセス
                blockContext.RegisterOperationAction(
                    operationContext =>
                    {
                        var op = (IPropertyReferenceOperation)operationContext.Operation;
                        if (PurityRulesEngine.IsIoPropertyAccess(op))
                        {
                            operationContext.ReportDiagnostic(Diagnostic.Create(
                                RT0003,
                                op.Syntax.GetLocation(),
                                method.Name));
                        }
                    },
                    OperationKind.PropertyReference);

                // RT0002/RT0003: メソッド呼び出し
                blockContext.RegisterOperationAction(
                    operationContext =>
                    {
                        var op = (IInvocationOperation)operationContext.Operation;
                        var violation = PurityRulesEngine.CheckInvocation(op, pureAttributeType);
                        switch (violation)
                        {
                            case InvocationViolation.IoOperation:
                                operationContext.ReportDiagnostic(Diagnostic.Create(
                                    RT0003,
                                    op.Syntax.GetLocation(),
                                    method.Name));
                                break;
                            case InvocationViolation.NonPureCall:
                                operationContext.ReportDiagnostic(Diagnostic.Create(
                                    RT0002,
                                    op.Syntax.GetLocation(),
                                    method.Name,
                                    op.TargetMethod.Name));
                                break;
                        }
                    },
                    OperationKind.Invocation);
            });
        });
    }

    private static bool HasPureAttribute(IMethodSymbol method, INamedTypeSymbol pureAttributeType)
    {
        foreach (var attr in method.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, pureAttributeType))
                return true;
        }
        return false;
    }
}

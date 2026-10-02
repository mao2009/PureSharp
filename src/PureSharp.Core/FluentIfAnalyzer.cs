using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PureSharp.Core;
using PureSharp.Core.Resources;

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
        title: new LocalizableResourceString(nameof(DiagnosticResources.FIF0001_Title), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        messageFormat: new LocalizableResourceString(nameof(DiagnosticResources.FIF0001_MessageFormat), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)),
        category: "FluentIf",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(nameof(DiagnosticResources.FIF0001_Description), DiagnosticResources.ResourceManager, typeof(DiagnosticResources)));

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
            var fluentType = compilationContext.Compilation
                .GetTypeByMetadataName("PureSharp.Core.Fluent");
            var conditionResultType = compilationContext.Compilation
                .GetTypeByMetadataName("PureSharp.Core.ConditionResult`1");
            var conditionActionType = compilationContext.Compilation
                .GetTypeByMetadataName("PureSharp.Core.ConditionAction");

            if (fluentType is null || (conditionResultType is null && conditionActionType is null))
                return;

            compilationContext.RegisterSyntaxNodeAction(
                ctx => CheckInvocation(ctx, fluentType, conditionResultType, conditionActionType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void CheckInvocation(
        SyntaxNodeAnalysisContext ctx,
        INamedTypeSymbol fluentType,
        INamedTypeSymbol? conditionResultType,
        INamedTypeSymbol? conditionActionType)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var method = ctx.SemanticModel.GetSymbolInfo(invocation, ctx.CancellationToken).Symbol as IMethodSymbol;

        // Analyze only an actual PureSharp.Core.Fluent.If entry point. This avoids reporting on
        // arbitrary expressions whose return type merely happens to be ConditionResult<T>.
        if (method is null ||
            method.Name != "If" ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, fluentType))
        {
            return;
        }

        var terminalInvocation = FindTerminalInvocation(invocation);

        // A bare Fluent.If(...) is a valid C# expression but an incomplete FluentIf chain.
        if (ReferenceEquals(terminalInvocation, invocation))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(FIF0001, invocation.GetLocation()));
            return;
        }

        var terminalMethod = ctx.SemanticModel
            .GetSymbolInfo(terminalInvocation, ctx.CancellationToken)
            .Symbol as IMethodSymbol;

        // If the outer invocation itself cannot be bound, the compiler already owns the error.
        // Avoid adding a misleading FIF0001 while the user is typing an invalid/partial chain.
        if (terminalMethod is null)
            return;

        if (terminalMethod.Name == "Else" &&
            IsConditionType(terminalMethod.ContainingType, conditionResultType, conditionActionType))
        {
            return;
        }

        // The chain continued, but did not terminate in the FluentIf Else API. This catches
        // cases such as Fluent.If(...).ToString() that the old return-type-based check missed.
        ctx.ReportDiagnostic(Diagnostic.Create(FIF0001, terminalInvocation.GetLocation()));
    }

    private static InvocationExpressionSyntax FindTerminalInvocation(InvocationExpressionSyntax start)
    {
        ExpressionSyntax current = start;
        var terminal = start;

        while (true)
        {
            // Parentheses must not break chain recognition:
            // ((Fluent.If(...))).Else(...)
            if (current.Parent is ParenthesizedExpressionSyntax parenthesized)
            {
                current = parenthesized;
                continue;
            }

            if (current.Parent is MemberAccessExpressionSyntax memberAccess &&
                ReferenceEquals(memberAccess.Expression, current) &&
                memberAccess.Parent is InvocationExpressionSyntax nextInvocation &&
                ReferenceEquals(nextInvocation.Expression, memberAccess))
            {
                terminal = nextInvocation;
                current = nextInvocation;
                continue;
            }

            return terminal;
        }
    }

    private static bool IsConditionType(
        INamedTypeSymbol containingType,
        INamedTypeSymbol? conditionResultType,
        INamedTypeSymbol? conditionActionType)
    {
        if (conditionResultType is not null &&
            SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, conditionResultType))
        {
            return true;
        }

        return conditionActionType is not null &&
            SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, conditionActionType);
    }
}

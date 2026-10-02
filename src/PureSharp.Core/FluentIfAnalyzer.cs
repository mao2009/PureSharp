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

        ExpressionSyntax current = invocation;
        var lastFluentInvocation = invocation;

        while (true)
        {
            var nextInvocation = FindNextInvocation(current);
            if (nextInvocation is null)
            {
                // The Fluent.If chain ended without reaching Else. A bare Fluent.If(...) and an
                // ElseIf-only chain are both valid C# expressions, so FIF0001 owns this error.
                ctx.ReportDiagnostic(Diagnostic.Create(FIF0001, lastFluentInvocation.GetLocation()));
                return;
            }

            var nextMethod = ctx.SemanticModel
                .GetSymbolInfo(nextInvocation, ctx.CancellationToken)
                .Symbol as IMethodSymbol;

            // If the chained invocation cannot be bound, the compiler already owns the error.
            // Avoid adding a misleading FIF0001 while the user is typing invalid/partial code.
            if (nextMethod is null)
                return;

            if (nextMethod.Name == "Else" &&
                IsConditionType(nextMethod.ContainingType, conditionResultType, conditionActionType))
            {
                // The FluentIf chain is complete at Else. Anything after the Else result belongs
                // to the caller's ordinary expression chain and must not be treated as FluentIf.
                return;
            }

            if (!IsConditionType(nextMethod.ContainingType, conditionResultType, conditionActionType))
            {
                // An unrelated method was invoked on the intermediate condition object before
                // Else, e.g. Fluent.If(...).ToString(). That consumes the chain without its
                // required terminator.
                ctx.ReportDiagnostic(Diagnostic.Create(FIF0001, nextInvocation.GetLocation()));
                return;
            }

            lastFluentInvocation = nextInvocation;
            current = nextInvocation;
        }
    }

    private static InvocationExpressionSyntax? FindNextInvocation(ExpressionSyntax current)
    {
        ExpressionSyntax expression = current;

        // Parentheses must not break chain recognition:
        // ((Fluent.If(...))).Else(...)
        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
            expression = parenthesized;

        if (expression.Parent is MemberAccessExpressionSyntax memberAccess &&
            ReferenceEquals(memberAccess.Expression, expression) &&
            memberAccess.Parent is InvocationExpressionSyntax nextInvocation &&
            ReferenceEquals(nextInvocation.Expression, memberAccess))
        {
            return nextInvocation;
        }

        return null;
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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PureSharp.Analyzers;

/// <summary>
/// 純粋性ルールの検証ロジックをカプセル化するクラス。
/// </summary>
internal static class PurityRulesEngine
{
    private static readonly ImmutableHashSet<string> KnownPureTypeNames = ImmutableHashSet.Create(
        "System.Math",
        "System.MathF",
        "System.Convert",
        "System.String",
        "string",
        "System.Char",
        "char",
        "System.Boolean",
        "bool",
        "System.Byte",
        "byte",
        "System.SByte",
        "sbyte",
        "System.Int16",
        "short",
        "System.UInt16",
        "ushort",
        "System.Int32",
        "int",
        "System.UInt32",
        "uint",
        "System.Int64",
        "long",
        "System.UInt64",
        "ulong",
        "System.Single",
        "float",
        "System.Double",
        "double",
        "System.Decimal",
        "decimal",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.TimeSpan",
        "System.Guid",
        "System.Object",
        "object",
        "System.Linq.Enumerable",
        "System.Enum",
        "System.Func",
        "System.Action"
    );

    public static bool IsIoType(string? typeName)
    {
        if (typeName is null) return false;
        return typeName.StartsWith("System.Console") ||
               typeName.StartsWith("System.IO.") ||
               typeName.StartsWith("System.Net.") ||
               typeName.StartsWith("NLog.") ||
               typeName.StartsWith("Microsoft.Extensions.Logging.");
    }

    private static bool IsKnownPureType(string? typeName)
    {
        if (typeName is null) return false;
        if (KnownPureTypeNames.Contains(typeName)) return true;

        return typeName.StartsWith("System.Func") ||
               typeName.StartsWith("System.Action") ||
               typeName.StartsWith("System.Predicate") ||
               typeName.StartsWith("System.Comparison");
    }

    /// <summary>
    /// 対象のシンボルが「不変」として扱われるローカル変数か否かを判定します。
    /// </summary>
    /// <param name="symbol">判定対象のシンボル</param>
    /// <returns>不変として扱われるべき場合 true、それ以外は false</returns>
    public static bool IsPureLocalVariable(ISymbol? symbol)
    {
        if (symbol is not ILocalSymbol local) return false;

        // 単一のアンダースコアは discard 記号として扱われるため除外
        if (local.Name == "_") return false;

        // アンダースコアで始まる変数名を不変の対象とする
        return local.Name.StartsWith("_");
    }

    /// <summary>
    /// 不変ローカル変数が宣言時に初期化されていない (LVP0002) をチェックします。
    /// </summary>
    public static bool IsMissingInitializer(IVariableDeclaratorOperation operation)
    {
        if (!IsPureLocalVariable(operation.Symbol)) return false;

        // Initializer が null の場合は未初期化
        return operation.Initializer is null;
    }

    /// <summary>
    /// 不変ローカル変数への再代入 (LVP0001) をチェックします。
    /// </summary>
    public static bool IsReassignmentToPureLocal(IOperation operation)
    {
        ILocalSymbol? targetLocal = null;

        if (operation is IAssignmentOperation assignment)
        {
            if (assignment.Target is ILocalReferenceOperation localRef)
                targetLocal = localRef.Local;
        }
        else if (operation is ICompoundAssignmentOperation compound)
        {
            if (compound.Target is ILocalReferenceOperation localRef)
                targetLocal = localRef.Local;
        }
        else if (operation is IIncrementOrDecrementOperation incDec)
        {
            if (incDec.Target is ILocalReferenceOperation localRef)
                targetLocal = localRef.Local;
        }

        return IsPureLocalVariable(targetLocal);
    }

    /// <summary>
    /// static かつ readonly でないフィールドアクセス (RT0001) をチェックします。
    /// </summary>
    public static bool IsStaticMutableFieldAccess(IFieldReferenceOperation operation)
    {
        var field = operation.Field;
        return field.IsStatic && !field.IsReadOnly && !field.IsConst;
    }

    /// <summary>
    /// I/O 関連の型へのプロパティアクセス (RT0003) をチェックします。
    /// </summary>
    public static bool IsIoPropertyAccess(IPropertyReferenceOperation operation)
    {
        var containingType = operation.Property.ContainingType?.ToDisplayString();
        return IsIoType(containingType);
    }

    /// <summary>
    /// メソッド呼び出しが I/O 操作か (RT0003)、または非純粋メソッド呼び出しか (RT0002) をチェックします。
    /// </summary>
    public static InvocationViolation CheckInvocation(
        IInvocationOperation operation,
        INamedTypeSymbol pureAttributeType)
    {
        var targetMethod = operation.TargetMethod;
        var containingTypeName = targetMethod.ContainingType?.ToDisplayString();

        if (IsIoType(containingTypeName))
            return InvocationViolation.IoOperation;

        if (IsKnownPureType(containingTypeName))
            return InvocationViolation.None;

        // [PureMethod] アトリビュートが付与されているかをチェック
        foreach (var attr in targetMethod.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, pureAttributeType))
                return InvocationViolation.None;
        }

        return InvocationViolation.NonPureCall;
    }
}

internal enum InvocationViolation
{
    None,
    IoOperation,
    NonPureCall,
}

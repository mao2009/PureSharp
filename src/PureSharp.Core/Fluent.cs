using System;

namespace PureSharp.Core;

public static class Fluent
{
    /// <summary>条件が true の場合に <paramref name="func"/> を実行し、値を返す FluentIf チェーンを開始します。</summary>
    [PureMethod]
    public static ConditionResult<T> If<T>(bool condition, Func<T> func)
    {
        return condition
            ? new ConditionResult<T>(true, func())
            : new ConditionResult<T>(false, default);
    }

    /// <summary>条件が true の場合に <paramref name="action"/> を実行する FluentIf チェーンを開始します。</summary>
    public static ConditionAction If(bool condition, Action action)
    {
        if (condition) action();
        return new ConditionAction(condition);
    }
}

/// <summary>
/// <see cref="Func{T}"/> を受け取る FluentIf チェーンの中間状態を表します。
/// すべてのメソッドは参照透過であり、<see cref="PureMethodAttribute"/> が付与されています。
/// </summary>
public class ConditionResult<T>
{
    private readonly bool _isResolved;
    private readonly T? _value;

    internal ConditionResult(bool isResolved, T? value)
    {
        _isResolved = isResolved;
        _value = value;
    }

    /// <summary>前の条件が未確定の場合に、追加条件を評価します。</summary>
    [PureMethod]
    public ConditionResult<T> ElseIf(bool condition, Func<T> func)
    {
        if (_isResolved) return this;
        return condition
            ? new ConditionResult<T>(true, func())
            : this;
    }

    /// <summary>いずれの条件も一致しなかった場合に <paramref name="func"/> の結果を返します。</summary>
    [PureMethod]
    public T Else(Func<T> func) => _isResolved ? _value! : func();

    /// <summary>いずれの条件も一致しなかった場合に <paramref name="elseValue"/> を返します。</summary>
    [PureMethod]
    public T Else(T elseValue) => _isResolved ? _value! : elseValue;
}

/// <summary>
/// <see cref="Action"/> を受け取る FluentIf チェーンの中間状態を表します。
/// 副作用を許容するため、<see cref="PureMethodAttribute"/> は付与されていません。
/// </summary>
public class ConditionAction
{
    private readonly bool _isResolved;

    internal ConditionAction(bool isResolved)
    {
        _isResolved = isResolved;
    }

    /// <summary>前の条件が未確定の場合に、追加条件を評価し、一致した場合に <paramref name="action"/> を実行します。</summary>
    public ConditionAction ElseIf(bool condition, Action action)
    {
        if (_isResolved) return this;
        if (condition) action();
        return new ConditionAction(condition);
    }

    /// <summary>いずれの条件も一致しなかった場合に <paramref name="action"/> を実行します。</summary>
    public void Else(Action action)
    {
        if (!_isResolved) action();
    }
}

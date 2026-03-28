using System;

namespace PureSharp.Core;

/// <summary>
/// メソッドが純粋（参照透過）であることを宣言するためのアトリビュート。
/// このアトリビュートが付与されたメソッドは、静的状態へのアクセス、I/O 操作、
/// および非純粋メソッドの呼び出しを行ってはなりません。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PureMethodAttribute : Attribute
{
}

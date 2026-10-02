using System;
using PureSharp.Core;
using Xunit;

namespace PureSharp.Analyzers.Tests;

public class FluentRuntimeTests
{
    [Fact]
    public void Result_TrueBranch_EvaluatesOnlySelectedBranch()
    {
        var ifCalls = 0;
        var elseCalls = 0;

        var result = Fluent.If(true, () => { ifCalls++; return 10; })
            .Else(() => { elseCalls++; return 20; });

        Assert.Equal(10, result);
        Assert.Equal(1, ifCalls);
        Assert.Equal(0, elseCalls);
    }

    [Fact]
    public void Result_FalseBranch_EvaluatesElseOnly()
    {
        var ifCalls = 0;
        var elseCalls = 0;

        var result = Fluent.If(false, () => { ifCalls++; return 10; })
            .Else(() => { elseCalls++; return 20; });

        Assert.Equal(20, result);
        Assert.Equal(0, ifCalls);
        Assert.Equal(1, elseCalls);
    }

    [Fact]
    public void ElseIf_FirstMatch_ShortCircuitsLaterBranches()
    {
        var firstElseIfCalls = 0;
        var secondElseIfCalls = 0;
        var elseCalls = 0;

        var result = Fluent.If(false, () => 1)
            .ElseIf(true, () => { firstElseIfCalls++; return 2; })
            .ElseIf(true, () => { secondElseIfCalls++; return 3; })
            .Else(() => { elseCalls++; return 4; });

        Assert.Equal(2, result);
        Assert.Equal(1, firstElseIfCalls);
        Assert.Equal(0, secondElseIfCalls);
        Assert.Equal(0, elseCalls);
    }

    [Fact]
    public void Action_FalseBranch_ExecutesElseOnly()
    {
        var ifCalls = 0;
        var elseCalls = 0;

        Fluent.If(false, () => ifCalls++)
            .Else(() => elseCalls++);

        Assert.Equal(0, ifCalls);
        Assert.Equal(1, elseCalls);
    }

    [Fact]
    public void SelectedBranchException_PropagatesUnchanged()
    {
        var expected = new InvalidOperationException("selected branch failed");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            Fluent.If(true, (Func<int>)(() => throw expected)).Else(0));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void UnselectedBranchException_IsNotObserved()
    {
        var result = Fluent.If(false, (Func<int>)(() => throw new InvalidOperationException()))
            .Else(42);

        Assert.Equal(42, result);
    }
}

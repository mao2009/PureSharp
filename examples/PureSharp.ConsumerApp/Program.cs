using PureSharp.Core;
using System;

class Program
{
    private static int _globalCache;

    // Example 1: RT0001 - Static field access
    // This will trigger RT0001 when [PureMethod] is applied
    [PureMethod]
    public static int AddWithGlobalField(int a, int b)
    {
        _globalCache = a + b;  // RT0001: Accessing static mutable field
        return _globalCache;
    }

    // Example 2: LVP0001 / LVP0002 - Immutable local variables
    public static void TestImmutableVariables()
    {
        int _result = 10;
        // Uncommenting below would trigger LVP0001 (reassignment to immutable variable)
        // _result = 20;

        // Example of variable that could trigger LVP0003 (naming suggestion)
        // if not reassigned:
        int count = 0;
    }

    // Example 3: FIF0001 - FluentIf termination
    public static void TestFluentIf()
    {
        int status = Fluent.If(true, () => 1)
                            .Else(() => 0);

        // Without .Else(), would trigger FIF0001
        // int incomplete = Fluent.If(true, () => 1);
    }

    static void Main()
    {
        Console.WriteLine("PureSharp Consumer App - Diagnostic Severity Configuration Test");
        Console.WriteLine("See .editorconfig for severity settings");
    }
}

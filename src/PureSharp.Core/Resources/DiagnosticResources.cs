using System.Globalization;
using System.Reflection;
using System.Resources;

namespace PureSharp.Core.Resources;

internal static class DiagnosticResources
{
    private static readonly ResourceManager resourceManager = new ResourceManager(
        "PureSharp.Core.Resources.DiagnosticResources",
        typeof(DiagnosticResources).Assembly);

    public static ResourceManager ResourceManager => resourceManager;

    public const string RT0001_Title = nameof(RT0001_Title);
    public const string RT0001_MessageFormat = nameof(RT0001_MessageFormat);
    public const string RT0001_Description = nameof(RT0001_Description);

    public const string RT0002_Title = nameof(RT0002_Title);
    public const string RT0002_MessageFormat = nameof(RT0002_MessageFormat);
    public const string RT0002_Description = nameof(RT0002_Description);

    public const string RT0003_Title = nameof(RT0003_Title);
    public const string RT0003_MessageFormat = nameof(RT0003_MessageFormat);
    public const string RT0003_Description = nameof(RT0003_Description);

    public const string LVP0001_Title = nameof(LVP0001_Title);
    public const string LVP0001_MessageFormat = nameof(LVP0001_MessageFormat);
    public const string LVP0001_Description = nameof(LVP0001_Description);

    public const string LVP0002_Title = nameof(LVP0002_Title);
    public const string LVP0002_MessageFormat = nameof(LVP0002_MessageFormat);
    public const string LVP0002_Description = nameof(LVP0002_Description);

    public const string LVP0003_Title = nameof(LVP0003_Title);
    public const string LVP0003_MessageFormat = nameof(LVP0003_MessageFormat);
    public const string LVP0003_Description = nameof(LVP0003_Description);

    public const string FIF0001_Title = nameof(FIF0001_Title);
    public const string FIF0001_MessageFormat = nameof(FIF0001_MessageFormat);
    public const string FIF0001_Description = nameof(FIF0001_Description);
}

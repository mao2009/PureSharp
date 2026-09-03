using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace PureSharp.Analyzers.Tests;

/// <summary>
/// 公開 Diagnostic 契約の回帰テスト。
///
/// docs/DIAGNOSTICS.md が定義する SSOT を機械的に固定する。ID / category /
/// default severity / 既定有効であることは v1.0 の公開契約であり、これらを変更する
/// PR は必ずこのテストを更新しなければならない。
///
/// message 文言そのものは契約対象外のため値を固定しない。代わりに「各 descriptor が
/// 自分自身の ID に対応する resource key を参照していること」を検証する。
/// </summary>
public class DiagnosticContractTests
{
    /// <summary>v1.0 で公開契約とする全 Diagnostic。docs/DIAGNOSTICS.md の要約表と一致する。</summary>
    public static readonly IReadOnlyList<(string Id, string Category, DiagnosticSeverity Severity)> PublicContract =
        new[]
        {
            ("RT0001", "Purity", DiagnosticSeverity.Error),
            ("RT0002", "Purity", DiagnosticSeverity.Error),
            ("RT0003", "Purity", DiagnosticSeverity.Error),
            ("LVP0001", "Purity", DiagnosticSeverity.Error),
            ("LVP0002", "Purity", DiagnosticSeverity.Error),
            ("LVP0003", "Naming", DiagnosticSeverity.Warning),
            ("FIF0001", "FluentIf", DiagnosticSeverity.Error),
        };

    public static TheoryData<string, string, DiagnosticSeverity> ContractRows()
    {
        var data = new TheoryData<string, string, DiagnosticSeverity>();
        foreach (var (id, category, severity) in PublicContract)
            data.Add(id, category, severity);
        return data;
    }

    private static IReadOnlyList<DiagnosticAnalyzer> AllAnalyzers() => new DiagnosticAnalyzer[]
    {
        new ReferentialTransparencyAnalyzer(),
        new LocalVariablePurityAnalyzer(),
        new ImmutableNamingSuggestionAnalyzer(),
        new FluentIfAnalyzer(),
    };

    private static IReadOnlyList<DiagnosticDescriptor> AllDescriptors() =>
        AllAnalyzers().SelectMany(a => a.SupportedDiagnostics).ToList();

    private static DiagnosticDescriptor DescriptorFor(string id) =>
        AllDescriptors().Single(d => d.Id == id);

    // =========================================================
    // 契約: ID / category / default severity / 既定有効
    // =========================================================

    [Theory]
    [MemberData(nameof(ContractRows))]
    public void Descriptor_MatchesPublicContract(string id, string category, DiagnosticSeverity severity)
    {
        var descriptor = DescriptorFor(id);

        Assert.Equal(id, descriptor.Id);
        Assert.Equal(category, descriptor.Category);
        Assert.Equal(severity, descriptor.DefaultSeverity);
        Assert.True(descriptor.IsEnabledByDefault, $"{id} must be enabled by default.");
    }

    [Fact]
    public void SupportedDiagnostics_ContainsExactlyThePublicContract()
    {
        var actual = AllDescriptors().Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var expected = PublicContract.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        // 追加・削除の両方を検出する。新しい Diagnostic は docs/DIAGNOSTICS.md と
        // このリストを同時に更新しなければ通らない。
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Descriptor_IdsAreUnique()
    {
        var ids = AllDescriptors().Select(d => d.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    // =========================================================
    // 契約: ID 命名規則
    // =========================================================

    [Theory]
    [MemberData(nameof(ContractRows))]
    public void DiagnosticId_FollowsNamingConvention(string id, string category, DiagnosticSeverity severity)
    {
        _ = category;
        _ = severity;

        // PREFIX + 4 桁連番 (docs/DIAGNOSTICS.md)
        Assert.Matches("^(RT|LVP|FIF)[0-9]{4}$", id);
    }

    // =========================================================
    // 契約: descriptor が自分自身の resource key を参照していること
    //
    // ID / category / severity が正しくても description だけ別 Diagnostic の
    // resource を指しているという不整合を検出する。
    // =========================================================

    [Theory]
    [MemberData(nameof(ContractRows))]
    public void Descriptor_TextResolvesAndIsNonEmpty(string id, string category, DiagnosticSeverity severity)
    {
        _ = category;
        _ = severity;

        var descriptor = DescriptorFor(id);

        Assert.False(string.IsNullOrWhiteSpace(descriptor.Title.ToString(CultureInfo.InvariantCulture)),
            $"{id} title must resolve to a non-empty string.");
        Assert.False(string.IsNullOrWhiteSpace(descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture)),
            $"{id} message format must resolve to a non-empty string.");
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.ToString(CultureInfo.InvariantCulture)),
            $"{id} description must resolve to a non-empty string.");
    }

    [Fact]
    public void Descriptor_DescriptionsAreNotSharedBetweenDiagnostics()
    {
        // LVP0003 の description が FIF0001_Description を参照していた不具合の回帰テスト。
        // 説明文の使い回しは、利用者に他の Diagnostic の説明を表示してしまう。
        var descriptions = AllDescriptors()
            .Select(d => (d.Id, Text: d.Description.ToString(CultureInfo.InvariantCulture)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .ToList();

        var duplicated = descriptions
            .GroupBy(x => x.Text, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(", ", g.Select(x => x.Id)))
            .ToList();

        Assert.True(duplicated.Count == 0,
            "These diagnostics share an identical description, which means at least one " +
            "references another diagnostic's resource key: " + string.Join(" | ", duplicated));
    }

    [Fact]
    public void Descriptor_TitlesAreNotSharedBetweenDiagnostics()
    {
        var titles = AllDescriptors()
            .Select(d => (d.Id, Text: d.Title.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        var duplicated = titles
            .GroupBy(x => x.Text, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(", ", g.Select(x => x.Id)))
            .ToList();

        Assert.True(duplicated.Count == 0,
            "These diagnostics share an identical title: " + string.Join(" | ", duplicated));
    }

    // =========================================================
    // 契約: category 方針
    // =========================================================

    [Theory]
    [MemberData(nameof(ContractRows))]
    public void Category_SeverityPolicyIsRespected(string id, string category, DiagnosticSeverity severity)
    {
        _ = id;

        // docs/DIAGNOSTICS.md: Naming は既定でビルドを止めない。
        if (category == "Naming")
            Assert.True(severity < DiagnosticSeverity.Error,
                "Naming diagnostics must not default to Error.");
        else
            Assert.Equal(DiagnosticSeverity.Error, severity);
    }

    [Fact]
    public void Category_UsesOnlyDocumentedCategories()
    {
        var documented = new[] { "Purity", "Naming", "FluentIf" };
        foreach (var descriptor in AllDescriptors())
            Assert.Contains(descriptor.Category, documented);
    }
}

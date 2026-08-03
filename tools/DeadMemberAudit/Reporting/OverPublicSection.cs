using DeadMemberAudit.Loading;
using DeadMemberAudit.Model;
using System.Text;

namespace DeadMemberAudit.Reporting;

// リスト3。参照は実在するが公開範囲が広すぎるメンバーを、縮小先ごとに書き出す
// List 3: members that are referenced yet exposed too widely, split by how far they can be narrowed
public sealed class OverPublicSection
{
    private const int AggregationRows = 30;
    private const int InlineTableLimit = 60;

    private readonly AssemblyClassifier _classifier;

    public OverPublicSection(AssemblyClassifier classifier)
    {
        _classifier = classifier;
    }

    public void Append(StringBuilder builder, AuditResult result)
    {
        builder.AppendLine("## リスト3-A: 公開範囲過剰（private候補・全参照が宣言型の中だけ）");
        builder.AppendLine();
        AppendTable(builder, result.PrivateCandidates);

        builder.AppendLine("## リスト3-B: 公開範囲過剰（internal候補・全参照が宣言アセンブリの中だけ）");
        builder.AppendLine();

        // internal候補は件数が多いので、まず型単位の集約で読ませ、全件は折りたたみに入れる
        // Internal candidates are numerous, so a per-type aggregation leads and the full list is collapsed
        if (result.InternalCandidates.Count > InlineTableLimit)
        {
            AppendAggregation(builder, result.InternalCandidates);
            builder.AppendLine($"<details><summary>全{result.InternalCandidates.Count}件</summary>");
            builder.AppendLine();
            AppendTable(builder, result.InternalCandidates);
            builder.AppendLine("</details>");
            builder.AppendLine();
            return;
        }

        AppendTable(builder, result.InternalCandidates);
    }

    private void AppendTable(StringBuilder builder, IReadOnlyList<MemberCandidate> candidates)
    {
        builder.AppendLine("| アセンブリ | メンバー | 宣言型 | 宣言場所 | 参照元 |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var candidate in candidates)
        {
            var sources = candidate.ReferencesByAssembly
                .OrderByDescending(entry => entry.Value)
                .Select(entry => $"{entry.Key}({_classifier.Classify(entry.Key)}):{entry.Value}");
            builder.AppendLine($"| {candidate.AssemblyName} | `{candidate.DisplayName}` — `{candidate.Signature}` | `{candidate.DeclaringTypeFullName}` | {ReportCells.Location(candidate.SourceLocation)} | {string.Join(", ", sources)} |");
        }

        builder.AppendLine();
    }

    private static void AppendAggregation(StringBuilder builder, IReadOnlyList<MemberCandidate> candidates)
    {
        var groups = candidates
            .GroupBy(candidate => candidate.DeclaringTypeFullName, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Take(AggregationRows)
            .ToList();

        builder.AppendLine($"### 宣言型ごとの集約（上位{groups.Count}型）");
        builder.AppendLine();
        builder.AppendLine("| 宣言型 | 候補数 |");
        builder.AppendLine("| --- | ---: |");
        foreach (var group in groups) builder.AppendLine($"| `{group.Key}` | {group.Count()} |");
        builder.AppendLine();
    }
}

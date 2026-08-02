using DeadMemberAudit.Analysis;
using DeadMemberAudit.Loading;
using DeadMemberAudit.Model;
using System.Text;

namespace DeadMemberAudit.Reporting;

// 監査結果をmarkdownに整形する
// Formats the audit result as markdown
public sealed class MarkdownReportWriter
{
    private readonly AssemblyClassifier _classifier;

    public MarkdownReportWriter(AssemblyClassifier classifier)
    {
        _classifier = classifier;
    }

    public string Write(AuditResult result, string assembliesDirectory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 死にpublicメンバー監査レポート");
        builder.AppendLine();
        builder.AppendLine($"- 解析対象: `{assembliesDirectory}`");
        builder.AppendLine($"- 生成日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();

        AppendSummary(builder, result);
        AppendNeverReferenced(builder, result);
        AppendNonProductionOnly(builder, result);
        AppendCaveats(builder);
        return builder.ToString();
    }

    private static void AppendSummary(StringBuilder builder, AuditResult result)
    {
        builder.AppendLine("## サマリ");
        builder.AppendLine();
        builder.AppendLine("| 項目 | 件数 |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| 走査したメソッド本体 | {result.ScannedMethodCount} |");
        builder.AppendLine($"| 母集団（productionのpublicメンバー） | {result.PopulationCount} |");
        builder.AppendLine($"| 機械除外 合計 | {result.ExcludedTotal()} |");
        builder.AppendLine($"| production参照あり（生存） | {result.LiveCount} |");
        builder.AppendLine($"| リスト1: 参照0 | {result.NeverReferenced.Count} |");
        builder.AppendLine($"| リスト2: 非production参照のみ | {result.NonProductionOnly.Count} |");
        builder.AppendLine($"| シンボル無しで読んだアセンブリ | {result.SymbolLessAssemblyCount} |");
        builder.AppendLine($"| 読み込めなかったDLL | {result.SkippedFileCount} |");
        builder.AppendLine();

        // アセンブリ分類の内訳。production以外からの参照は生存根拠にしない
        // Breakdown of assembly classification; references from non-production do not count as life
        builder.AppendLine("### アセンブリ分類");
        builder.AppendLine();
        builder.AppendLine("| 分類 | アセンブリ数 |");
        builder.AppendLine("| --- | ---: |");
        foreach (var entry in result.AssemblyCountsByCategory.OrderBy(entry => entry.Key.ToString(), StringComparer.Ordinal))
        {
            builder.AppendLine($"| {entry.Key} | {entry.Value} |");
        }

        builder.AppendLine();
        builder.AppendLine("### 除外内訳");
        builder.AppendLine();
        builder.AppendLine("| 除外理由 | 件数 |");
        builder.AppendLine("| --- | ---: |");
        foreach (var entry in result.ExclusionCounts.OrderByDescending(entry => entry.Value))
        {
            builder.AppendLine($"| {entry.Key} | {entry.Value} |");
        }

        builder.AppendLine();
    }

    private static void AppendNeverReferenced(StringBuilder builder, AuditResult result)
    {
        builder.AppendLine("## リスト1: 参照0（どのアセンブリからも呼ばれていない）");
        builder.AppendLine();
        builder.AppendLine("| アセンブリ | メンバー | 宣言型 | 宣言場所 |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var candidate in result.NeverReferenced)
        {
            builder.AppendLine($"| {candidate.AssemblyName} | `{candidate.DisplayName}` — `{candidate.Signature}` | `{candidate.DeclaringTypeFullName}` | {FormatLocation(candidate)} |");
        }

        builder.AppendLine();
    }

    private void AppendNonProductionOnly(StringBuilder builder, AuditResult result)
    {
        builder.AppendLine("## リスト2: テスト/デバッグ/エディタ/デフォルト参照のみ");
        builder.AppendLine();
        builder.AppendLine("| アセンブリ | メンバー | 宣言型 | 宣言場所 | 参照元 |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var candidate in result.NonProductionOnly)
        {
            var sources = candidate.ReferencesByAssembly
                .OrderByDescending(entry => entry.Value)
                .Select(entry => $"{entry.Key}({_classifier.Classify(entry.Key)}):{entry.Value}");
            builder.AppendLine($"| {candidate.AssemblyName} | `{candidate.DisplayName}` — `{candidate.Signature}` | `{candidate.DeclaringTypeFullName}` | {FormatLocation(candidate)} | {string.Join(", ", sources)} |");
        }

        builder.AppendLine();
    }

    // シンボルが無いメンバーは位置が空になる。表が崩れないようハイフンで埋める
    // Members without symbols have no location, so a hyphen keeps the table intact
    private static string FormatLocation(MemberCandidate candidate)
    {
        return candidate.SourceLocation.Length > 0 ? $"`{candidate.SourceLocation}`" : "-";
    }

    // 自動削除を禁じる注意書き。ILに現れない呼び出し経路が実在する
    // The warning against automatic deletion, because call routes that never appear in IL do exist
    private static void AppendCaveats(StringBuilder builder)
    {
        builder.AppendLine("## 注意");
        builder.AppendLine();
        builder.AppendLine("**このリストは削除候補であって、自動削除してはならない。** 以下の呼び出し経路はILに現れないため、検出漏れが原理的に残る。");
        builder.AppendLine();
        builder.AppendLine("- UnityEventのシーン/Prefab配線（`m_MethodName`による文字列参照）");
        builder.AppendLine("- `uloop execute-dynamic-code` およびプレイテストシナリオ`.cs`からの呼び出し");
        builder.AppendLine("- `GetMethod`/`GetProperty`/`GetField`等の文字列リフレクション");
        builder.AppendLine("- `Activator.CreateInstance`による動的生成（外部Mod DLLを含む）");
        builder.AppendLine("- OneJS/JavaScript側からのバインディング呼び出し");
        builder.AppendLine();
        builder.AppendLine("削除前に、対象メンバー名でリポジトリ全体（`.prefab`/`.unity`/`.cs`スニペット/`.js`）をgrepして裏を取ること。");
        builder.AppendLine();
    }
}

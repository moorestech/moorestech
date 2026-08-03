using DeadMemberAudit.Cancellation;
using DeadMemberAudit.Model;
using System.Text;

namespace DeadMemberAudit.Reporting;

// リスト5。トークン未伝搬・async void・CTS作りっぱなしの3形を書き出す
// List 5: the three cancellation shapes — dropped tokens, async void, and dangling CTS
public static class CancellationSection
{
    public static void Append(StringBuilder builder, AuditResult result)
    {
        AppendIssue(builder, result, CancellationIssue.TokenNotPassed,
            "## リスト5-A: CancellationToken未伝搬（呼び出し元はトークンを持っている）", "呼び出し");
        AppendIssue(builder, result, CancellationIssue.AsyncVoid,
            "## リスト5-B: async void（Unityイベント関数は除外済み）", "メソッド");
        AppendIssue(builder, result, CancellationIssue.CancellationTokenSourceNotReleased,
            "## リスト5-C: CancellationTokenSource作りっぱなし（Cancel/Disposeが無い）", "フィールド");
    }

    private static void AppendIssue(StringBuilder builder, AuditResult result, CancellationIssue issue, string heading, string subjectColumn)
    {
        builder.AppendLine(heading);
        builder.AppendLine();
        builder.AppendLine($"| アセンブリ | {subjectColumn} | 宣言型 | 宣言場所 | 形 |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var finding in result.CancellationFindings.Where(finding => finding.Issue == issue))
        {
            builder.AppendLine($"| {finding.AssemblyName} | `{finding.MemberDisplay}` | `{finding.DeclaringTypeFullName}` | {ReportCells.Location(finding.SourceLocation)} | {finding.Detail} |");
        }

        builder.AppendLine();
    }
}

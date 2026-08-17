using DeadMemberAudit.Model;
using DeadMemberAudit.PrivateHelper;
using System.Text;

namespace DeadMemberAudit.Reporting;

// リスト6。privateメソッドを「畳む候補」と「消す候補」の2表に分けて書き出す
// List 6: private methods split into two tables, the folding candidates and the deletion candidates
public static class PrivateHelperSection
{
    public static void Append(StringBuilder builder, AuditResult result)
    {
        builder.AppendLine("## リスト6-A: 単一呼び出し元privateヘルパ（呼び出し元の `#region Internal` ローカル関数へ畳む候補）");
        builder.AppendLine();
        builder.AppendLine("| アセンブリ | メソッド | 宣言型 | 宣言場所 | 唯一の呼び出し元 |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var finding in result.PrivateHelperFindings.Where(finding => finding.Issue == PrivateHelperIssue.SingleCaller))
        {
            builder.AppendLine($"| {finding.AssemblyName} | `{finding.DisplayName}` — `{finding.Signature}` | `{finding.DeclaringTypeFullName}` | {ReportCells.Location(finding.SourceLocation)} | `{finding.CallerDisplayName}` |");
        }

        builder.AppendLine();

        // 呼び出し元0のprivateは外から呼びようがないので、リスト1のpublicより削除の確度が高い
        // A private with no caller cannot be reached from outside, so deletion is safer here than for the public members of list 1
        builder.AppendLine("## リスト6-B: 参照0privateメソッド（同一型内のどこからも呼ばれていない）");
        builder.AppendLine();
        builder.AppendLine("| アセンブリ | メソッド | 宣言型 | 宣言場所 | 呼び出し元 |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var finding in result.PrivateHelperFindings.Where(finding => finding.Issue == PrivateHelperIssue.NeverCalled))
        {
            builder.AppendLine($"| {finding.AssemblyName} | `{finding.DisplayName}` — `{finding.Signature}` | `{finding.DeclaringTypeFullName}` | {ReportCells.Location(finding.SourceLocation)} | IL上に呼び出し元なし |");
        }

        builder.AppendLine();
    }
}

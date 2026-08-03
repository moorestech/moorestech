using DeadMemberAudit.Model;
using DeadMemberAudit.Placement;
using System.Text;

namespace DeadMemberAudit.Reporting;

// リスト4。server側で宣言されたのにserver側に解決者がいない型を書き出す
// List 4: types declared on the server side that have no resolver there
public static class PlacementSection
{
    public static void Append(StringBuilder builder, AuditResult result)
    {
        builder.AppendLine("## リスト4-A: サーバー配置ミス（server宣言・client側からしか使われていない）");
        builder.AppendLine();
        builder.AppendLine("| アセンブリ | 型 | 宣言場所 | server実装 | client参照元 |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var finding in result.PlacementFindings.Where(finding => finding.Issue == PlacementIssue.ClientOnlyUsage))
        {
            builder.AppendLine($"| {finding.AssemblyName} | `{finding.TypeFullName}` | {ReportCells.Location(finding.SourceLocation)} | {ReportCells.OrDash(finding.ServerImplementation)} | {ReportCells.OrDash(finding.ClientUsage)} |");
        }

        builder.AppendLine();

        // DI登録だけが接点の型は、移設より先に「誰も解決していない」ことが問題になる
        // For types whose only touch point is registration, the missing resolver matters before any relocation
        builder.AppendLine("## リスト4-B: サーバー配置ミス（DI登録のみ・server側に解決者なし）");
        builder.AppendLine();
        builder.AppendLine("| アセンブリ | 型 | 宣言場所 | client参照元 | server実装 | server登録元 |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var finding in result.PlacementFindings.Where(finding => finding.Issue == PlacementIssue.RegistrationWithoutResolver))
        {
            builder.AppendLine($"| {finding.AssemblyName} | `{finding.TypeFullName}` | {ReportCells.Location(finding.SourceLocation)} | {ReportCells.OrDash(finding.ClientUsage)} | {ReportCells.OrDash(finding.ServerImplementation)} | {ReportCells.OrDash(finding.ServerRegistration)} |");
        }

        builder.AppendLine();
    }
}

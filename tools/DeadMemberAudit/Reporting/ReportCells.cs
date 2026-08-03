namespace DeadMemberAudit.Reporting;

// 表のセル整形。ゲートは `path:line` をバッククォート込みで拾うため、書式を1箇所に集める
// Table cell formatting; the gate scrapes a backticked `path:line`, so the format lives in one place
public static class ReportCells
{
    // 位置が取れないメンバーは空になる。表が崩れないようハイフンで埋める
    // Members without a location come out empty, so a hyphen keeps the table intact
    public static string Location(string sourceLocation)
    {
        return sourceLocation.Length > 0 ? $"`{sourceLocation}`" : "-";
    }

    // 参照元が無い列は空セルだと読みにくいのでハイフンにする
    // An empty referencing column reads poorly, so it becomes a hyphen
    public static string OrDash(string value)
    {
        return value.Length > 0 ? value : "-";
    }
}

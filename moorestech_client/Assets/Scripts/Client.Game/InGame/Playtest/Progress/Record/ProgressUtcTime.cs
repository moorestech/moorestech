using System;
using System.Globalization;
using Game.Paths;

namespace Client.Game.InGame.Playtest.Progress.Record
{
    // 外部成果物へ出す日時の綴りと復元を1本にする。書き側と読み側でカルチャが割れると同じ瞬間が別表記になる（ADR 0060 裁定3）
    // One spelling and one restoration for the timestamps in external artifacts; a culture split writes one instant two ways (ADR 0060 adjudication 3)
    public static class ProgressUtcTime
    {
        // ISO文字列はUTC固定。地方時と解釈されると記録全体が数時間ずれる
        // The ISO strings are always UTC; reading them as local time would shift the whole record by hours
        private const DateTimeStyles IsoStyles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

        public static string ToIso(DateTime utc)
        {
            return utc.ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture);
        }

        public static bool TryParseIso(string iso, out DateTime utc)
        {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, IsoStyles, out utc);
        }
    }
}

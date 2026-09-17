using System.Collections.Generic;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.Game.InGame.Playtest.Progress.Record;
using Client.Game.InGame.Playtest.Progress.Record.Events;

namespace Client.Game.InGame.Playtest.Progress.Storage
{
    // current/ の1セッションを1回だけ読んだ中身。閉じる手順はこれを使い回し、同じファイルを2度読まない（C22）
    // One session in current/ read exactly once; closing reuses it and never reads the same files twice (C22)
    internal sealed class ProgressSessionContents
    {
        public readonly ProgressRecordHeader Header;
        public readonly IReadOnlyList<IProgressEvent> Events;

        private ProgressSessionContents(ProgressRecordHeader header, IReadOnlyList<IProgressEvent> events)
        {
            Header = header;
            Events = events;
        }

        // イベントが読めなければ失敗を返す。空として畳むと current/ が消えて記録が1件黙って失われる（F06）
        // Unreadable events come back as a failure; folding them as empty would clear current/ and silently lose one record (F06)
        public static SalvageOperationResult Load(string sessionDirectory, out ProgressSessionContents contents)
        {
            contents = null;
            var read = BugReportFileOperations.ReadLines(ProgressRecordPaths.EventsPathIn(sessionDirectory), out var lines);
            if (!read.Succeeded) return SalvageOperationResult.Failure($"進行記録のイベントを読めなかった: {read.FailureReason}");

            var events = new List<IProgressEvent>();
            var brokenLineCount = 0;
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                var progressEvent = ProgressEventLine.FromJsonLine(line);
                if (progressEvent == null) brokenLineCount++;
                else events.Add(progressEvent);
            }

            // 捨てた行は件数を欠損として記録へ載せる。無音で消すと読み手は欠けた記録を完全なものと読む
            // Dropped lines reach the record as a counted gap; vanishing silently would let a reader take a partial record as complete
            var header = ReadHeader(sessionDirectory, events);
            if (0 < brokenLineCount) header.AddMissing(ProgressRecordPaths.EventsFileName, $"読めないイベント行を捨てた count:{brokenLineCount}");
            contents = new ProgressSessionContents(header, events);
            return SalvageOperationResult.Success();
        }

        // ヘッダを失った残骸でもイベントは救う。埋められない値は欠損として理由付きで残し、既定値へ黙って落とさない
        // A leftover that lost its header still yields its events; unfillable values are declared as gaps with reasons instead of silently defaulting
        private static ProgressRecordHeader ReadHeader(string sessionDirectory, List<IProgressEvent> events)
        {
            var read = BugReportFileOperations.ReadText(ProgressRecordPaths.HeaderPathIn(sessionDirectory), out var json);
            var header = read.Succeeded ? ProgressRecordHeader.FromJson(json) : null;
            if (header != null) return header;

            var cause = read.Succeeded ? "中身が壊れている" : read.FailureReason;
            var lostHeader = new ProgressRecordHeader { SessionStart = 0 < events.Count ? events[0].T : null };
            lostHeader.AddMissing("header", $"ヘッダが無い（または壊れている）ため steamId・worldCreatedAt・baseline を埋められない events:{events.Count} cause:{cause}");
            return lostHeader;
        }
    }
}

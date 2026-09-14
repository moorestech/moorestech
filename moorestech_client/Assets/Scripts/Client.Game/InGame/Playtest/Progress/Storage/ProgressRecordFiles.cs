using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行中セッションを current/pid_<PID>/ に追記し、終了時に outbox の1箱へ畳む。追記形式なので落ちても直前まで残る
    // Appends the in-flight session under current/pid_<PID>/ and folds it into one outbox box at the end; append-only survives a crash
    public static class ProgressRecordFiles
    {
        public static SalvageOperationResult WriteHeader(string sessionDirectory, ProgressRecordHeader header)
        {
            return ProgressDiskIo.WriteText(ProgressRecordPaths.HeaderPathIn(sessionDirectory), header.ToJson());
        }

        public static SalvageOperationResult OpenEventAppender(string sessionDirectory, out StreamWriter appender)
        {
            return ProgressDiskIo.OpenAppender(ProgressRecordPaths.EventsPathIn(sessionDirectory), out appender);
        }

        // ヘッダが無く events.jsonl だけの残骸も1セッションぶんの残骸。見落とすと次の記録へ黙って混ざる
        // A leftover with only events.jsonl and no header is still one session's leftover; missing it silently mixes into the next record
        public static bool HasCurrentSession(string sessionDirectory)
        {
            return File.Exists(ProgressRecordPaths.HeaderPathIn(sessionDirectory)) || File.Exists(ProgressRecordPaths.EventsPathIn(sessionDirectory));
        }

        public static ProgressRecordHeader ReadHeader(string sessionDirectory)
        {
            var read = ProgressDiskIo.ReadText(ProgressRecordPaths.HeaderPathIn(sessionDirectory), out var json);
            if (!read.Succeeded) return null;
            return ProgressRecordHeader.FromJson(json);
        }

        // 壊れた行は捨てるが件数は返す。捨てた事実を欠損として記録へ載せるため
        // Broken lines are dropped but counted, so the fact that they were dropped reaches the record as a gap
        public static List<ProgressEventEntry> ReadEvents(string sessionDirectory, out int brokenLineCount)
        {
            brokenLineCount = 0;
            var entries = new List<ProgressEventEntry>();
            var read = ProgressDiskIo.ReadLines(ProgressRecordPaths.EventsPathIn(sessionDirectory), out var lines);
            if (!read.Succeeded)
            {
                Debug.LogError($"進行記録のイベントを読めませんでした: {read.FailureReason}");
                return entries;
            }

            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                var entry = ProgressEventEntry.FromJsonLine(line);
                if (entry == null) brokenLineCount++;
                else entries.Add(entry);
            }
            return entries;
        }

        // 閉じられなければ null。呼び出し側は理由をログへ出す（無音で消さない）
        // Returns null when it cannot close; the caller logs the reason and never drops it silently
        public static string CloseCurrentInto(string sessionDirectory, string endReason, DateTime sessionEndUtc, IReadOnlyList<MissingItem> extraMissing)
        {
            if (!HasCurrentSession(sessionDirectory)) return null;

            var events = ReadEvents(sessionDirectory, out var brokenLineCount);
            var header = ReadHeader(sessionDirectory) ?? CreateHeaderForLostHeader(events);
            if (brokenLineCount > 0) header.AddMissing(ProgressRecordPaths.EventsFileName, $"読めないイベント行を捨てた count:{brokenLineCount}");
            foreach (var item in extraMissing) header.Missing.Add(item);

            var directory = BugReportOutbox.CreateBundleDirectory(GameSystemPaths.ProgressRecordOutboxDirectory, DateTime.UtcNow, BugReportOutbox.CreateShortId());
            var write = ProgressDiskIo.WriteText(Path.Combine(directory, ProgressRecordPaths.RecordFileName), ProgressRecordComposer.Compose(header, events, endReason, sessionEndUtc));
            if (!write.Succeeded)
            {
                Debug.LogError($"進行記録を書けませんでした（この記録は運搬されません） {directory}: {write.FailureReason}");
                return null;
            }

            // READY を置いてから current/ を消す。逆順だと運搬されない箱だけが残って記録が1件消える
            // READY is placed before current/ is cleared; the reverse order would leave an unshippable box and lose one record
            BugReportOutbox.MarkReady(directory);
            ClearCurrent(sessionDirectory);
            Debug.Log($"進行記録を書きました {directory} endReason:{endReason} missing:{header.Missing.Count}");
            return directory;
        }

        // ヘッダを失った残骸でもイベントは救う。埋められない値は欠損として理由付きで残し、既定値へ黙って落とさない
        // A leftover that lost its header still yields its events; unfillable values are declared as gaps with reasons instead of silently defaulting
        private static ProgressRecordHeader CreateHeaderForLostHeader(List<ProgressEventEntry> events)
        {
            var header = new ProgressRecordHeader { SessionStart = events.Count > 0 ? events[0].T : "" };
            header.AddMissing("header", $"ヘッダが無い（または壊れている）ため steamId・worldCreatedAt・baseline を埋められない events:{events.Count}");
            return header;
        }

        public static void ClearCurrent(string sessionDirectory)
        {
            var headerDeletion = SalvageFileOperations.DeleteFile(ProgressRecordPaths.HeaderPathIn(sessionDirectory));
            if (!headerDeletion.Succeeded) Debug.LogError($"進行記録のヘッダを消せませんでした（次回起動で偽の記録が出ます）: {headerDeletion.FailureReason}");

            var eventsDeletion = SalvageFileOperations.DeleteFile(ProgressRecordPaths.EventsPathIn(sessionDirectory));
            if (!eventsDeletion.Succeeded) Debug.LogError($"進行記録のイベントを消せませんでした（次回起動の記録へ混ざります）: {eventsDeletion.FailureReason}");
        }
    }
}

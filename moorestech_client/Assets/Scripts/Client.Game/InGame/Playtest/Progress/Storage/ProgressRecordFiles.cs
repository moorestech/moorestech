using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.Game.InGame.Playtest.Progress.Record;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress.Storage
{
    // 進行中セッションを current/pid_<PID>/session_<utcTicks>/ に追記し、終了時に outbox の1箱へ畳む。追記形式なので落ちても直前まで残る
    // Appends the in-flight session under current/pid_<PID>/session_<utcTicks>/ and folds it into one outbox box at the end; append-only survives a crash
    internal static class ProgressRecordFiles
    {
        public static SalvageOperationResult WriteHeader(string sessionDirectory, ProgressRecordHeader header)
        {
            return BugReportFileOperations.WriteText(ProgressRecordPaths.HeaderPathIn(sessionDirectory), header.ToJson());
        }

        public static SalvageOperationResult OpenEventAppender(string sessionDirectory, out StreamWriter appender)
        {
            return BugReportFileOperations.OpenAppender(ProgressRecordPaths.EventsPathIn(sessionDirectory), out appender);
        }

        // ヘッダが無く events.jsonl だけの残骸も1セッションぶんの残骸。見落とすと次の記録へ黙って混ざる
        // A leftover with only events.jsonl and no header is still one session's leftover; missing it silently mixes into the next record
        public static bool HasCurrentSession(string sessionDirectory)
        {
            return File.Exists(ProgressRecordPaths.HeaderPathIn(sessionDirectory)) || File.Exists(ProgressRecordPaths.EventsPathIn(sessionDirectory));
        }

        // 今回のセッションを閉じる。終了時刻は呼び出し側が知っている
        // Closes this session; the caller knows the end time
        public static ProgressCloseResult CloseCurrentInto(string sessionDirectory, ProgressEndReason endReason, DateTime sessionEndUtc)
        {
            if (!HasCurrentSession(sessionDirectory)) return ProgressCloseResult.NothingToClose();
            if (!TryLoad(sessionDirectory, out var contents)) return ProgressCloseResult.Failed();
            return FoldIntoBundle(sessionDirectory, contents, endReason, sessionEndUtc);
        }

        // 前回セッションの残骸を閉じる。1回だけ読んだ中身から終了時刻を解決し、代用した出所を必ず sessionEnd の欠損として名乗る（C22 / C19）
        // Closes a previous session's leftover; the end time is resolved from contents read once, and its substitute source is always declared as a sessionEnd gap (C22 / C19)
        public static ProgressCloseResult CloseLeftoverInto(string sessionDirectory, ProgressEndReason endReason, IReadOnlyList<MissingItem> extraMissing)
        {
            if (ProgressHandOffMarker.TryFindShippedBundle(sessionDirectory, out var shippedBundle))
            {
                Debug.Log($"前回の進行記録は箱 {shippedBundle} へ渡し済みのため、箱は作り直さず current/ だけを片付けます");
                ClearCurrent(sessionDirectory);
                return ProgressCloseResult.Closed(shippedBundle);
            }

            if (!HasCurrentSession(sessionDirectory)) return ProgressCloseResult.NothingToClose();
            if (!TryLoad(sessionDirectory, out var contents)) return ProgressCloseResult.Failed();
            foreach (var item in extraMissing) contents.Header.Missing.Add(item);
            return FoldIntoBundle(sessionDirectory, contents, endReason, ResolveLeftoverSessionEnd(contents));
        }

        private static bool TryLoad(string sessionDirectory, out ProgressSessionContents contents)
        {
            var load = ProgressSessionContents.Load(sessionDirectory, out contents);
            if (load.Succeeded) return true;
            Debug.LogError($"進行記録を読めないため閉じません（current/ に残し次回起動で回収し直します） {sessionDirectory}: {load.FailureReason}");
            return false;
        }

        // 異常終了した残骸は終了時刻を持たない。最後のイベント→開始時刻→回収時刻の順に代用し、どれを使ったかを必ず残す
        // A crashed leftover has no end time; the last event, then the start, then the recovery moment stand in, and which one was used is always recorded
        private static DateTime ResolveLeftoverSessionEnd(ProgressSessionContents contents)
        {
            var header = contents.Header;
            var events = contents.Events;
            if (0 < events.Count && ProgressUtcTime.TryParseIso(events[events.Count - 1].T, out var lastEvent))
            {
                header.AddMissing("sessionEnd", $"終了時刻が記録されていないため最後のイベント時刻で代用した value:{events[events.Count - 1].T}");
                return lastEvent;
            }
            if (ProgressUtcTime.TryParseIso(header.SessionStart, out var sessionStart))
            {
                header.AddMissing("sessionEnd", $"終了時刻が記録されておらず時刻の読めるイベントも無いため開始時刻で代用した value:{header.SessionStart}");
                return sessionStart;
            }
            header.AddMissing("sessionEnd", $"終了時刻も開始時刻も読めないため回収時刻で代用した sessionStart:{header.SessionStart}");
            return DateTime.UtcNow;
        }

        // 閉じられなかった理由は「中身が無い」と「書けなかった」で分けて返す。書けなかった記録は current/ に残る
        // The failure to close comes back split into "nothing there" and "could not write"; an unwritten record stays in current/
        private static ProgressCloseResult FoldIntoBundle(string sessionDirectory, ProgressSessionContents contents, ProgressEndReason endReason, DateTime sessionEndUtc)
        {
            var creation = BugReportFileOperations.CreateBundleDirectory(GameSystemPaths.ProgressRecordOutboxDirectory, out var directory);
            if (!creation.Succeeded)
            {
                Debug.LogError($"進行記録の箱を作れませんでした（この記録は current/ に残り次回起動で回収されます）: {creation.FailureReason}");
                return ProgressCloseResult.Failed();
            }

            var record = ProgressRecordComposer.Compose(contents.Header, contents.Events, endReason, sessionEndUtc);
            var write = BugReportFileOperations.WriteText(Path.Combine(directory, ProgressRecordPaths.RecordFileName), record);
            if (!write.Succeeded) return AbandonBundle("進行記録を書けませんでした", write.FailureReason);

            // 箱を確定する前に current/ 側へ受け渡しの印を置く。READY と current/ の削除の間で落ちても次回起動は作り直さない（F18）
            // The hand-off mark goes into current/ before the box is finalized, so a crash between READY and clearing current/ is never rebuilt next boot (F18)
            var handOff = ProgressHandOffMarker.Write(sessionDirectory, directory);
            if (!handOff.Succeeded) return AbandonBundle("進行記録の受け渡し印を置けませんでした", handOff.FailureReason);

            var ready = BugReportFileOperations.WriteText(Path.Combine(directory, BugReportOutbox.ReadyMarkerFileName), "");
            if (!ready.Succeeded) return AbandonBundle("進行記録のREADYを置けませんでした", ready.FailureReason);

            ClearCurrent(sessionDirectory);
            Debug.Log($"進行記録を書きました {directory} endReason:{ProgressEndReasonJson.ToContractText(endReason)} missing:{contents.Header.Missing.Count}");
            return ProgressCloseResult.Closed(directory);

            #region Internal

            // 閉じられなかった箱はREADYが無く運搬されないが、消す者も居ないとoutboxへ溜まり続ける。記録本体はcurrent/に残るので消して困らない
            // An unclosed box has no READY and never ships, but with nobody deleting it the outbox only grows; the record itself stays in current/, so dropping the box costs nothing
            ProgressCloseResult AbandonBundle(string failure, string reason)
            {
                Debug.LogError($"{failure}（current/ は残し次回起動の回収へ回します） {directory}: {reason}");
                var deletion = BugReportDiskOperations.DeleteDirectory(directory);
                if (!deletion.Succeeded) Debug.LogWarning($"閉じられなかった進行記録の箱を消せませんでした（運搬はされませんがoutboxに残ります） {directory}: {deletion.FailureReason}");
                return ProgressCloseResult.Failed();
            }

            #endregion
        }

        // セッションの段ごと消す。受け渡し印だけが残ると、次回起動がそれを拾い続ける
        // Removes the whole session level; a hand-off mark left behind alone would be picked up on every later boot
        private static void ClearCurrent(string sessionDirectory)
        {
            var deletion = BugReportDiskOperations.DeleteDirectory(sessionDirectory);
            if (!deletion.Succeeded)
            {
                Debug.LogError($"進行記録の current/ を消せませんでした（受け渡し印が残っていれば次回起動は箱を作り直しません） {sessionDirectory}: {deletion.FailureReason}");
                return;
            }

            var parentDeletion = BugReportDiskOperations.DeleteDirectoryIfEmpty(Path.GetDirectoryName(sessionDirectory));
            if (!parentDeletion.Succeeded) Debug.LogWarning($"空になった進行記録の pid ディレクトリを消せませんでした {sessionDirectory}: {parentDeletion.FailureReason}");
        }
    }
}
